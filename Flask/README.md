# 스마트 컨베이어 모니터링 시스템 - Flask 서버

## 구조
```
project/
├── app.py               # 메인 Flask 서버 (HTTP:5000 + TCP:9000)
├── config.py            # 설정
├── models.py            # DB 모델 (SQLite)
├── tcp_server.py        # MFC ↔ Flask TCP 통신 (포트 9000)
├── socket_handlers.py   # WebSocket 핸들러
├── requirements.txt     # 의존성
├── routes/
│   ├── api.py           # REST API (/api/status, /api/control 등)
│   ├── auth.py          # 로그인/로그아웃
│   └── admin.py         # 관리자
└── templates/
    └── dashboard.html   # 웹 대시보드 (온도/습도/컨베이어/MFC 상태)
```

## 실행 방법

### 1. Flask 서버 (PC)
```bash
pip install -r requirements.txt
python app.py
```
- HTTP/WebSocket: http://0.0.0.0:5000
- TCP (MFC용):    0.0.0.0:9000
- 대시보드:       http://localhost:5000/

### 2. MFC 설정 (ConveyorProtocol.h)
MFC 프로젝트의 `ConveyorProtocol.h`에서 Flask 서버 PC의 IP로 수정:
```cpp
#define TCP_FLASK_HOST    "Flask서버IP"    // 예: "192.168.0.30"
#define TCP_FLASK_PORT    9000

#define HTTP_FLASK_HOST   "Flask서버IP"    // 예: "192.168.0.30"
#define HTTP_FLASK_PORT   5000
```

## 통신 흐름
```
  라즈베리파이(시리얼) → MFC ──HTTP POST /api/status──→ Flask ──WebSocket──→ 웹 브라우저
                         MFC ←──TCP 9000 (명령)────────← Flask ←──REST API──← 웹 브라우저
```

### MFC → Flask (3초마다)
- **HTTP POST /api/status**: 센서 데이터 전송 (temp, humi, distance, speed, fan, emg_stop)
- **TCP 9000**: 상시 연결 유지 (PING/PONG), 명령 수신

### Flask → 웹 브라우저 (실시간)
- **WebSocket 'sensor_update'**: 온도, 습도 실시간
- **WebSocket 'conveyor_status'**: 컨베이어 ON/OFF, 속도
- **WebSocket 'mfc_connection'**: MFC 연결/해제 이벤트

### 웹 → MFC (제어)
- **POST /api/control**: 웹에서 버튼 클릭 → Flask TCP → MFC
  - CONVEYOR:ON / CONVEYOR:OFF
  - EMERGENCY:STOP / EMERGENCY:RELEASE
  - SPEED:1 / SPEED:2 / SPEED:3
  - FAN:ON / FAN:OFF

## 웹 대시보드 표시 항목
1. **온도** (실시간, 히스토리 바 차트)
2. **습도** (실시간, 히스토리 바 차트)  
3. **컨베이어 ON/OFF** (속도 표시 포함)
4. **MFC 연결 상태** (CONNECTED / DISCONNECTED + 펄스 인디케이터)
5. **제어 패널** (컨베이어, 속도, 비상정지, 팬)
6. **시스템 로그** (실시간 이벤트)
7. **카메라 피드** (C# WinForm 분석 결과)

## 관리자 계정
- ID: admin / PW: 1234
