using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Diagnostics;
using System.Text;
using static Work1.DefectDetector;

namespace Work1
{
    public partial class Form1 : Form
    {
        // ==============================================================
        // ★ 설정값
        // ==============================================================
        static readonly string ONNX_MODEL = "best.onnx";
        static readonly string MONITOR_URL = "http://192.168.0.30:5000/predict";

        static readonly int CAM_WIDTH = 640;
        static readonly int CAM_HEIGHT = 480;
        static readonly int CAM_FPS = 60;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        // ==============================================================
        // 내부 변수
        // ==============================================================
        VideoCapture capture;
        Mat frame;
        System.Windows.Forms.Timer camTimer;
        DefectDetector detector;
        bool isProcessing = false;

        private static readonly HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(500) // 응답이 늦으면 과감히 포기
        };

        // FPS 계산
        int displayFpsCount = 0;
        float displayFps = 0;
        Stopwatch displayWatch = new Stopwatch();

        int detectFpsCount = 0;
        float detectFps = 0;
        Stopwatch detectWatch = new Stopwatch();

        int totalInspected = 0;
        int totalDefect = 0;

        // 상단 변수 구역에 추가
        int streamSkipCount = 0; // 네트워크 부하를 줄이기 위해 2~3프레임당 1번 전송용

        // ★ 캐시된 검출 결과 (AnalyzeFrame이 업데이트, UpdateFrame이 읽음)
        private readonly object _cacheLock = new object();
        private List<DetectBox> _displayBoxes = new();
        private bool _displayIsDefect = false;
        private bool _displayIsNormal = false;
        private List<(string name, float conf)> _displayDefectList = new();
        private string _displayMode = "WAIT";

        const int FABRIC_EXIT_FRAMES = 8;
        int fabricMissingCount = 0;

        FabricInfo? lockedFabricInfo = null;

        const double NEW_SAMPLE_CENTER_MOVE_PX = 45.0;
        const double NEW_SAMPLE_AREA_CHANGE_RATIO = 0.30;
        int newSampleChangeCount = 0;
        const int NEW_SAMPLE_CHANGE_FRAMES = 3;

        private int _stainHoldCount = 0;
        private List<DetectBox> _lastStainBoxes = new List<DetectBox>();
        private int _stainHoldTimer = 0;
        private List<DetectBox> _tempStainBoxes = new List<DetectBox>();
        private const int HOLD_LIMIT = 20; // 약 0.5초 유지
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 1) 검출기 로드
            try
            {
                string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ONNX_MODEL);
                if (!File.Exists(modelPath))
                {
                    MessageBox.Show(
                        $"ONNX 모델 파일이 없습니다!\n경로: {modelPath}\n\n" +
                        "1) python convert_to_onnx.py 실행\n" +
                        "2) best.onnx를 bin/Debug에 복사",
                        "모델 없음", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                detector = new DefectDetector(modelPath);
                this.Text = "Fabric Defect Detector — 모델 로드 완료";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"모델 로드 실패: {ex.Message}");
                return;
            }

            // 2) 카메라
            capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
            if (capture.IsOpened())
            {
                UpdateConnectionUI(true, "CONNECTED"); // 카메라 연결 성공
            }
            else
            {
                UpdateConnectionUI(false, "DISCONNECTED"); // 카메라 연결 실패
                MessageBox.Show("카메라 미연결");
                return;
            }

            capture.FrameWidth = CAM_WIDTH;
            capture.FrameHeight = CAM_HEIGHT;
            frame = new Mat();

            // 3) 타이머
            camTimer = new System.Windows.Forms.Timer();
            camTimer.Interval = 1000 / CAM_FPS;
            camTimer.Tick += UpdateFrame;
            camTimer.Start();

            displayWatch.Start();
            detectWatch.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cts.Cancel(); // 진행 중인 모든 통신 중단
            camTimer?.Stop();
            capture?.Release();
            frame?.Dispose();
            detector?.Dispose();
        }

        // ==============================================================
        // ★ 프레임 갱신 — 항상 30fps로 화면 갱신
        // 검출 결과는 캐시에서 읽어서 오버레이
        // ==============================================================
        private void UpdateFrame(object sender, EventArgs e)
        {
            if (capture == null || !capture.IsOpened() || detector == null) return;

            capture.Read(frame);
            if (frame.Empty()) return;

            // 1. 캐시 데이터 읽기
            List<DetectBox> boxes;
            bool isDefect;
            bool isNormal;
            List<(string name, float conf)> defectList;
            string mode;

            lock (_cacheLock)
            {
                boxes = _displayBoxes;
                isDefect = _displayIsDefect;
                isNormal = _displayIsNormal;
                defectList = _displayDefectList;
                mode = _displayMode;
            }

            // 2. FPS 계산
            displayFpsCount++;
            if (displayWatch.ElapsedMilliseconds >= 1000)
            {
                displayFps = displayFpsCount * 1000f / displayWatch.ElapsedMilliseconds;
                displayFpsCount = 0;
                displayWatch.Restart();
            }

            try
            {
                // 3. 화면 그리기 (딱 한 번만 실행)
                using var annotated = detector.DrawAnnotated(frame, boxes, isDefect, isNormal, defectList, mode);

                // UI 업데이트
                var oldImg = picCamera.Image;
                picCamera.Image = BitmapConverter.ToBitmap(annotated);
                oldImg?.Dispose();

                // 4. Flask 전송 (이미 그려진 annotated 이미지를 그대로 활용)
                streamSkipCount++;
                if (streamSkipCount >= 3) // 약 20fps로 전송
                {
                    streamSkipCount = 0;
                    // 이미지를 복제하여 비동기로 전송 (분석 루프 보호)
                    Mat sendFrame = annotated.Clone();
                    _ = Task.Run(() => SendLiveStreamOptimized(sendFrame, isDefect, mode));
                }

                SetStatus(isDefect,isNormal);
                this.Text = $"Fabric Defect — 화면:{displayFps:F0}fps 검출:{detectFps:F0}fps 검사:{totalInspected} 불량:{totalDefect}";
            }
            catch { }

            // 5. 검출 분석 (백그라운드)
            if (!isProcessing)
            {
                Mat frameCopy = frame.Clone();
                _ = Task.Run(() => AnalyzeFrame(frameCopy));
            }
        }
        private async Task SendLiveStreamOptimized(Mat mat, bool isDefect, string mode)
        {
            try
            {
                // 1. 이미지 인코딩 품질을 낮춰 전송 속도 극대화 (동기화의 핵심)
                byte[] jpg = mat.ToBytes(".jpg", new int[] { (int)ImwriteFlags.JpegQuality, 70 });
                string b64 = Convert.ToBase64String(jpg);

                var payload = new
                {
                    image_data = b64,
                    is_defective = isDefect,
                    status = mode,
                    timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
                };

                string json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");   

                // 2. 응답을 기다리되, 분석 루프와 분리되어 있으므로 전체 FPS에는 영향 없음
                var response = await httpClient.PostAsync(MONITOR_URL, content, _cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    // 통신 성공 시 상태 유지 (너무 자주 호출되면 UI 부하가 있으니 상태 확인 후 호출 권장)
                    if (lblConnStatus.Text != "CONNECTED") UpdateConnectionUI(true, "CONNECTED");
                }
                response.Dispose();
            }
            catch {
                UpdateConnectionUI(false, "SERVER ERROR");
            }
            finally
            {
                mat.Dispose(); // 복제했던 이미지 메모리 해제
            }
        }

        // ==============================================================
        // ★ 분석 — 백그라운드에서 실행, 결과를 캐시에 저장
        // 화면 갱신과 완전히 분리됨
        // ==============================================================

        int defectConfirmCount = 0;      // 연속 검출 카운트
        const int CONFIRM_THRESHOLD = 7; // 7프레임 연속 시 확정
        bool isSampleLocked = false;     // 현재 샘플 전송 완료 여부 (중복 방지)

        const int NORMAL_CHECK_FRAMES = 15;
        const int STABLE_THRESHOLD = 5;
        const double MOTION_DIFF_THRESHOLD = 8.0;
        const double MOTION_RATIO_THRESHOLD = 0.015;

        int stableFabricCount = 0;
        Mat? prevStableGray = null;
        int normalCheckCount = 0;
        bool sampleHadAnyDefect = false;
        FabricInfo? prevFabricInfo = null;

        private void AnalyzeFrame(Mat mat)
        {
            isProcessing = true;

            try
            {
                // 1. ROI 안에 실제 옷감이 있는지 확인
                // 검정~어두운 회색 레일은 DetectFabricInRoi()에서 제외됨
                var fabricInfo = detector.DetectFabricInRoi(mat);

                if (!fabricInfo.IsPresent)
                {
                    fabricMissingCount++;

                    defectConfirmCount = 0;
                    stableFabricCount = 0;
                    normalCheckCount = 0;
                    sampleHadAnyDefect = false;
                    prevStableGray?.Dispose();
                    prevStableGray = null;

                    // 옷감이 ROI에서 일정 프레임 이상 사라져야 다음 샘플 검사 가능
                    if (fabricMissingCount >= FABRIC_EXIT_FRAMES)
                    {
                        ResetForNextSample();
                    }

                    lock (_cacheLock)
                    {
                        _displayBoxes = new List<DetectBox>();
                        _displayIsDefect = false;
                        _displayIsNormal = false;
                        _displayDefectList = new List<(string name, float conf)>();
                        _displayMode = $"NO FABRIC {fabricMissingCount}/{FABRIC_EXIT_FRAMES}";
                    }

                    return;
                }
                else
                {
                    fabricMissingCount = 0;
                }

                // ★★★ 락 걸린 상태에서는 옷감이 빠질 때까지만 대기 ★★★
                // (원본의 위치/면적 변화 감지 로직 제거 — 같은 위치에서도 다음 샘플 인식 가능)
                if (isSampleLocked)
                {
                    lock (_cacheLock)
                    {
                        _displayMode = "WAIT SAMPLE OUT";
                    }
                    return;
                }

                // 2. 옷감이 정지했는지 판단
                bool isStable = IsFabricStableByFrameDiff(mat);

                stableFabricCount = isStable ? stableFabricCount + 1 : 0;
                bool fabricStopped = stableFabricCount >= STABLE_THRESHOLD;

                // 3. 불량 검사
                var (boxes, rawIsDefect,rawIsNormal, defectList, mode) = detector.ProcessFrame(mat);

                bool finalIsDefect = false;
                bool finalisNormal = false;

                if (boxes.Any(b => b.ClassName == "stain"))
                {
                    _stainHoldTimer = HOLD_LIMIT; // 발견 시 타이머 리셋
                    _tempStainBoxes = boxes.Where(b => b.ClassName == "stain").ToList();
                }
                else if (_stainHoldTimer > 0)
                {
                    _stainHoldTimer--;
                    // 발견되지 않아도 이전 프레임의 박스를 강제로 추가해서 깜빡임 방지
                    boxes.AddRange(_tempStainBoxes);
                }

                if (!fabricStopped)
                {
                    defectConfirmCount = 0;
                    normalCheckCount = 0;
                    sampleHadAnyDefect = false;
                    mode = "FABRIC MOVING";
                }
                else if (!isSampleLocked)
                {
                    if (rawIsDefect)
                    {
                        sampleHadAnyDefect = true;
                        defectConfirmCount++;
                        normalCheckCount = 0;
                    }
                    else
                    {
                        defectConfirmCount = 0;

                        if (!sampleHadAnyDefect)
                        {

                            normalCheckCount++;
                            mode = $"NORMAL CHECK {normalCheckCount}/{NORMAL_CHECK_FRAMES}";
                        }
                    }

                    if (defectConfirmCount >= CONFIRM_THRESHOLD)
                    {
                        finalIsDefect = true;
                    }
                }
                else
                {
                    mode = "WAIT SAMPLE OUT";
                }
                // 4. 화면 표시용 캐시 갱신
                lock (_cacheLock)
                {
                    _displayBoxes = boxes;
                    _displayIsDefect = finalIsDefect;
                    _displayIsNormal = finalisNormal;
                    _displayDefectList = defectList;
                    _displayMode = mode;
                }

                // 5. 불량 확정
                if (finalIsDefect && !isSampleLocked)
                {
                    isSampleLocked = true;
                    lockedFabricInfo = fabricInfo;
                    totalDefect++;
                    totalInspected++;

                    Mat defectCapture = mat.Clone();

                    _ = SendDefectToMonitor(mat, boxes, defectList, mode);
                    _ = SendDefectToFlask(defectList);

                    string defectTypes = string.Join(", ", defectList.Select(d => d.name).Distinct());
                    int defectCount = defectList.Count;

                    SetStatus(true,false);
                    AddLogToGrid(defectTypes, defectCount.ToString() + "개", true);
                    //AppendLog(defectTypes, defectCount);

                    Debug.WriteLine("🚨 불량 샘플 확정");
                }
                // 6. 정상 확정
                else if ( fabricStopped && !isSampleLocked && !sampleHadAnyDefect && normalCheckCount >= NORMAL_CHECK_FRAMES)
                {
                    isSampleLocked = true;
                    lockedFabricInfo = fabricInfo;
                    totalInspected++;
                    SetStatus(false,true);
                    _ = SendNormalToFlask();
                    AddLogToGrid("OK", "-", false);
                    //AppendNormalLog();

                    lock (_cacheLock)
                    {
                        _displayIsNormal = true;
                        _displayMode = "NORMAL CONFIRMED";
                    }

                    Debug.WriteLine("✅ 정상 샘플 확정");
                }

                detectFpsCount++;

                if (detectWatch.ElapsedMilliseconds >= 1000)
                {
                    detectFps = detectFpsCount * 1000f / detectWatch.ElapsedMilliseconds;
                    detectFpsCount = 0;
                    detectWatch.Restart();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"분석 에러: {ex.Message}");
            }
            finally
            {
                mat.Dispose();
                isProcessing = false;
            }
        }
        private void camTimer_Tick(object sender, EventArgs e)
        {
            if (isProcessing) return;
            if (capture == null || !capture.IsOpened()) return;

            using (Mat mat = new Mat())
            {
                if (!capture.Read(mat) || mat.Empty()) return;

                isProcessing = true;

                // 1. 검출 실행
                var result = detector.ProcessFrame(mat);
                var boxes = result.boxes; // 검출된 모든 박스 리스트

                // 2. [추가] Stain 잔상 처리 로직 적용
                var currentStains = boxes.Where(b => b.ClassName == "stain").ToList();
                if (currentStains.Count > 0)
                {
                    _stainHoldCount = HOLD_LIMIT; // 찾았으므로 카운트 리셋
                    _lastStainBoxes = currentStains;    // 위치 저장
                }
                else if (_stainHoldCount > 0)
                {
                    _stainHoldCount--; // 못 찾았으므로 카운트 감소
                    boxes.AddRange(_lastStainBoxes); // 저장해둔 이전 박스를 현재 결과에 강제로 추가
                }

                // 3. 이후 로직 (결과 표시 및 UI 업데이트)은 수정된 boxes를 사용
                // 기존 코드의 DrawBox(mat, boxes) 등이 이 아래에 오게 됩니다.

                // ... (기존 UI 업데이트 코드들) ...

                isProcessing = false;
            }
        }
        private void ResetForNextSample()
        {
            isSampleLocked = false;

            defectConfirmCount = 0;
            stableFabricCount = 0;
            normalCheckCount = 0;
            sampleHadAnyDefect = false;
            fabricMissingCount = 0;
            newSampleChangeCount = 0;

            lockedFabricInfo = null;

            prevStableGray?.Dispose();
            prevStableGray = null;

            detector.ResetState();

            lock (_cacheLock)
            {
                _displayBoxes = new List<DetectBox>();
                _displayIsDefect = false;
                _displayIsNormal = false;
                _displayDefectList = new List<(string name, float conf)>();
                _displayMode = "RESET";
            }
        }

        private bool IsFabricStableByFrameDiff(Mat mat)
        {
            var (rx1, ry1, rx2, ry2) = detector.GetCenterRoi(mat);

            using var roi = new Mat(mat, new Rect(rx1, ry1, rx2 - rx1, ry2 - ry1));
            using var gray = new Mat();

            Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);

            if (prevStableGray == null || prevStableGray.Empty())
            {
                prevStableGray?.Dispose();
                prevStableGray = gray.Clone();
                return false;
            }

            using var diff = new Mat();
            Cv2.Absdiff(prevStableGray, gray, diff);

            using var motionMask = new Mat();
            Cv2.Threshold(diff, motionMask, MOTION_DIFF_THRESHOLD, 255, ThresholdTypes.Binary);

            double motionPixels = Cv2.CountNonZero(motionMask);
            double totalPixels = Math.Max(1, motionMask.Width * motionMask.Height);
            double motionRatio = motionPixels / totalPixels;

            prevStableGray.Dispose();
            prevStableGray = gray.Clone();

            return motionRatio < MOTION_RATIO_THRESHOLD;
        }

        private async Task SendNormalToFlask()
        {
            try
            {
                var payload = new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string flaskUrl = MONITOR_URL.Replace(
                    "http://192.168.0.30:5000/predict",
                    "http://192.168.0.30:5000/api/defect"
                );

                await httpClient.PostAsync(flaskUrl, content);

                Debug.WriteLine("✅ 정상 샘플 전송 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 정상 샘플 전송 실패: {ex.Message}");
            }
        }

        private async Task SendDefectToMonitor(Mat mat, List<DetectBox> boxes, List<(string name, float conf)> defectList, string mode)
        {
            try
            {
                using var annotated = detector.DrawAnnotated(mat, boxes, true, false, defectList, mode);
                byte[] jpg = annotated.ToBytes(".jpg", new int[] { (int)ImwriteFlags.JpegQuality, 80 });
                string b64 = Convert.ToBase64String(jpg);

                var payload = new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    defect_type = defectList.FirstOrDefault().name ?? "Unknown",
                    confidence = defectList.FirstOrDefault().conf,
                    // C#은 경로를 모르므로 비워서 보냄. Flask가 결정함.
                    image_path = "",
                    data = new { image = b64 }
                };

                string json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _ = httpClient.PostAsync(MONITOR_URL, content);
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }
        // ==============================================================
        // ★ Flask /api/defect 로 불량 데이터 전송 → MySQL DB 저장
        //   전송 데이터: timestamp(결함 시간), defect_type(결함 유형), defect_count(결함 개수)
        // ==============================================================
        private async Task SendDefectToFlask(List<(string name, float conf)> defectList)
        {
            try
            {
                string defectTypes = string.Join(", ", defectList.Select(d => d.name).Distinct());
                int defectCount = defectList.Count;

                var payload = new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    defect_type = defectTypes,
                    defect_count = defectCount
                };

                string json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Flask 서버의 /api/defect 엔드포인트로 POST
                string flaskUrl = MONITOR_URL.Replace("http://192.168.0.30:5000/predict", "http://192.168.0.30:5000/api/defect");
                await httpClient.PostAsync(flaskUrl, content);

                Debug.WriteLine($"✅ [Flask DB] 불량 전송 완료 — 유형: {defectTypes}, 개수: {defectCount}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [Flask DB] 전송 실패: {ex.Message}");
            }
        }


        // ==============================================================
        // 상태 표시등
        // ==============================================================
        private void SetStatus(bool isDefective, bool isNormal)
        {
            // UI 컨트롤 접근을 위한 Invoke 확인 (스레드 안전)
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetStatus(isDefective, isNormal)));
                return;
            }

            if (isDefective)
            {
                // 1. 불량 검출 시
                lblStatusText.Text = "DEFECT";
                lblStatusText.ForeColor = Color.Red;
            }
            else if (isNormal)
            {
                // 2. 정상 확정 시
                lblStatusText.Text = "OK";
                lblStatusText.ForeColor = Color.LimeGreen;
            }
            else
            {
                // 3. 샘플이 없거나 분석 중인 대기 상태
                lblStatusText.Text = "WAIT";
                lblStatusText.ForeColor = Color.Gray;
            }
        }
        private void AddLogToGrid(string type, string count, bool isDefect)
        {
            if (dgv_Log.InvokeRequired)
            {
                dgv_Log.Invoke(new Action(() => AddLogToGrid(type, count, isDefect)));
                return;
            }

            // ★ 핵심: 변수에 대입하지 않고 바로 Insert 호출 (오류 방지)
            dgv_Log.Rows.Insert(0, DateTime.Now.ToString("HH:mm:ss"), type, count);

            // 첫 번째 행(방금 추가한 행)의 스타일 설정
            DataGridViewRow row = dgv_Log.Rows[0];

            if (isDefect)
            {
                // 불량 종류에 따른 상세 색상 설정
                Color defectColor;

                // 쉼표(,)가 포함된 "Thread,Hole" 같은 복합 불량도 처리하기 위해 분기
                if (type.Contains("Thread") && type.Contains("Hole"))
                {
                    defectColor = Color.MediumPurple; // 복합 불량은 보라색 계열 (예시)
                }
                else
                {
                    switch (type)
                    {
                        case "Thread":
                            defectColor = Color.FromArgb(255, 177, 66); // 주황색
                            break;
                        case "Hole":
                            defectColor = Color.FromArgb(255, 82, 82);  // 빨간색
                            break;
                        case "Stain":
                            defectColor = Color.LightSkyBlue;           // 하늘색 (예시)
                            break;
                        default:
                            defectColor = Color.HotPink;                // 정의되지 않은 기타 불량
                            break;
                    }
                }

                // 선택된 색상을 셀에 적용
                row.Cells[1].Style.ForeColor = defectColor; // 종류 열
                row.Cells[2].Style.ForeColor = defectColor; // 갯수 열
            }
            else
            {
                // 정상일 때 (normal 등)
                row.Cells[1].Style.ForeColor = Color.SpringGreen;
                row.Cells[2].Style.ForeColor = Color.SpringGreen;
            }
        }


        private void UpdateConnectionUI(bool isConnected, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateConnectionUI(isConnected, message)));
                return;
            }

            lblConnStatus.Text = message;

            if (isConnected)
            {
                pnlStatusDot.BackColor = Color.SpringGreen;
            }
            else
            {
                pnlStatusDot.BackColor = Color.OrangeRed;
            }
        }
    }
}
