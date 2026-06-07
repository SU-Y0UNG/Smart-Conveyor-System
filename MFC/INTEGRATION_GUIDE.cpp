// ================================================================
// INTEGRATION_GUIDE.cpp
// ================================================================
// VoiceAlert + ReportGenerator 기존 코드 통합 가이드
// ================================================================


// ================================================================
// [STEP 1] 프로젝트에 새 파일 추가 (4개)
// ================================================================
// Visual Studio > Solution Explorer > 우클릭 > Add > Existing Item:
//   - VoiceAlert.h / VoiceAlert.cpp
//   - ReportGenerator.h / ReportGenerator.cpp


// ================================================================
// [STEP 2] Conveyor_SystemDlg.h 수정
// ================================================================

// ── 2-1. #include "SettingsDlg.h" 아래에 추가 ──
#include "VoiceAlert.h"
#include "ReportGenerator.h"

// ── 2-2. private 섹션, AppSettings m_appSettings; 아래에 추가 ──
    CVoiceAlert         m_voiceAlert;
    CReportGenerator    m_reportGen;

// ── 2-3. private 섹션, void UpdateMonitorFrame(); 아래에 추가 ──
    void GenerateDailyReport();


// ================================================================
// [STEP 3] Conveyor_SystemDlg.cpp 수정
// ================================================================

// ── 3-1. OnInitDialog() — return TRUE; 직전에 추가 ──

    if (m_voiceAlert.Initialize())
        AddLog(_T("[Voice] TTS Initialized"));
    else
        AddLog(_T("[Voice] TTS Init Failed"));
    m_voiceAlert.SetCooldownSec(10);

    m_reportGen.SetDBManager(&m_dbManager);
    m_reportGen.SetOperatorName(m_strLoggedInUser);
    m_reportGen.SetOutputFolder(_T(".\\Reports"));


// ── 3-2. OnDestroy() — CDialogEx::OnDestroy(); 직전에 추가 ──

    m_voiceAlert.Shutdown();


// ── 3-3. ProcessStatusPacket() — DANGER 블록 ──
//   찾기:  m_dbManager.InsertAlert(m_fTemp, m_fHumi, "DANGER", buf);
//   바로 아래에 추가:
        m_voiceAlert.SpeakDanger(m_fTemp, m_fHumi);

// ── 3-4. ProcessStatusPacket() — WARNING 블록 ──
//   찾기:  m_dbManager.InsertAlert(m_fTemp, m_fHumi, "WARNING", buf);
//   바로 아래에 추가:
        m_voiceAlert.SpeakWarning(m_fTemp, m_fHumi);

// ── 3-5. OnBtnEmergencyStop() ──
//   찾기:  m_dbManager.InsertAlert(0, 0, "STOP", "Emergency stop button pressed");
//   바로 아래에 추가:
    m_voiceAlert.SpeakEmergency();

// ── 3-6. OnTcpReceived() — EMERGENCY:STOP 블록 ──
//   찾기:  m_dbManager.InsertAlert(0, 0, "STOP", "Emergency stop from Flask web");
//   바로 아래에 추가:
        m_voiceAlert.SpeakEmergency();


// ── 3-7. 파일 맨 아래에 함수 추가 ──

void CConveyorSystemDlg::GenerateDailyReport()
{
    m_reportGen.SetOperatorName(m_strLoggedInUser);
    CString filePath = m_reportGen.GenerateDailyReport();

    if (filePath.IsEmpty())
    {
        CString err;
        err.Format(_T("Report failed: %s"),
            (LPCTSTR)m_reportGen.GetLastError());
        AddLog(err);
        MessageBox(err, _T("Report Error"), MB_OK | MB_ICONERROR);
    }
    else
    {
        CString msg;
        msg.Format(_T("[Report] Saved: %s"), (LPCTSTR)filePath);
        AddLog(msg);
        MessageBox(filePath, _T("Report Generated"), MB_OK | MB_ICONINFORMATION);
        ShellExecute(NULL, _T("open"), filePath, NULL, NULL, SW_SHOWNORMAL);
    }
}


// ── 3-8. OnBtnLogout() 전체를 아래로 교체 ──

void CConveyorSystemDlg::OnBtnLogout()
{
    if (MessageBox(_T("Confirm logout?\nDaily report will be generated."),
        _T("Logout"), MB_YESNO | MB_ICONQUESTION) == IDYES)
    {
        GenerateDailyReport();
        AddLog(_T("[System] User logged out"));
        EndDialog(ID_LOGOUT_RESULT);
    }
}


// ================================================================
// [STEP 4] Conveyor_System.cpp — COM 초기화 확인
// ================================================================
// InitInstance()에 AfxOleInit() 호출이 있는지 확인.
// 없으면 InitInstance() 상단에 추가:
//
//   if (!AfxOleInit())
//   {
//       AfxMessageBox(_T("OLE Init Failed"));
//       return FALSE;
//   }
