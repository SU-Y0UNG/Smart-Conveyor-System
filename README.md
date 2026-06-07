# 🏭 Conveyor Belt Monitoring System — 스마트 팩토리 컨베이어 벨트 모니터링 시스템

<p align="center">
  <img src="https://img.shields.io/badge/C++-00599C?style=flat&logo=c%2B%2B&logoColor=white"/>
  <img src="https://img.shields.io/badge/MFC-5C2D91?style=flat&logo=visualstudio&logoColor=white"/>
  <img src="https://img.shields.io/badge/Flask-000000?style=flat&logo=flask&logoColor=white"/>
  <img src="https://img.shields.io/badge/C%23-239120?style=flat&logo=csharp&logoColor=white"/>
  <img src="https://img.shields.io/badge/Python-3776AB?style=flat&logo=python&logoColor=white"/>
  <img src="https://img.shields.io/badge/Raspberry%20Pi-A22846?style=flat&logo=raspberrypi&logoColor=white"/>
  <img src="https://img.shields.io/badge/SQLite-003B57?style=flat&logo=sqlite&logoColor=white"/>
  <img src="https://img.shields.io/badge/YOLOv8-00FFFF?style=flat&logo=yolo&logoColor=black"/>
</p>

<p align="center">
  MFC + Flask + Raspberry Pi + C# WinForm 기반 스마트 팩토리 시스템<br/>
  YOLOv8 불량 검출 · RS-232 장비 제어 · TCP/IP 실시간 통신 · 생산라인 모니터링을 통합한 컨베이어 벨트 관제 시스템입니다.
</p>

---

## 📌 프로젝트 개요

YOLOv8과 OpenCV를 활용하여 원단의 구멍, 실밥, 얼룩 등 불량 유형을 실시간으로 검출하고, 학습된 모델을 통해 불량 유형별 분류 정확도를 확보하였으며, 검출 결과에 따라 자동으로 양품과 불량품을 분리하도록 Raspberry Pi와 MFC를 연동하여 컨베이어 벨트 구동 및 불량품 분류 장치를 자동 제어하는 스마트 팩토리 시스템입니다.

MFC는 RS-232 Serial 통신으로 Raspberry Pi의 온습도 센서 데이터 및 비상 정지 신호를 수신하여 HTTP로 Flask에 전송하고, Flask에서 TCP/IP 통신을 통해 비상 정지 명령과 C# WinForm의 YOLO 검출 영상을 수신하여 MFC 모니터링 패널에 실시간 영상 스트리밍 기능을 구현하였습니다. 불량 이력은 SQLite에 저장하고 이벤트 로그 및 PDF 보고서로 출력할 수 있습니다.

Flask 서버를 통해 불량 데이터 저장 및 통계 조회, 생산라인 실시간 모니터링, 관리자 간 채팅 기능을 통합하여 현장 상태를 즉시 확인하고 이상 발생 시 빠르게 대응할 수 있도록 구현한 프로젝트입니다.

---

## ⚙️ 주요 기능

### 현장 관리자 (MFC C++)

| 기능 | 설명 |
|------|------|
| **실시간 모니터링** | Flask를 통해 수신한 YOLO 검출 영상을 MFC 패널에 실시간 렌더링 |
| **센서 데이터 수신** | RS-232로 Raspberry Pi의 온습도·적외선 센서 데이터를 수신·표시 |
| **컨베이어 제어** | 컨베이어 벨트 ON/OFF, 3단 속도 제어, 환풍기 제어 |
| **비상 정지** | 비상 정지 버튼 및 Flask 원격 비상 정지 명령 처리 |
| **음성 알림** | WARNING·DANGER·EMERGENCY 상황별 TTS 음성 경고 |
| **알림 이력 관리** | SQLite 기반 WARNING·DANGER·STOP 이력 저장 및 조회 |
| **PDF 보고서** | 일일 운영 리포트 PDF 자동 생성 및 출력 |
| **환경 설정** | CSliderCtrl/CComboBox 기반 임계값·통신·쿨다운 시간 설정 |
| **로그인** | 관리자 계정 인증 기반 접근 제어 |

### 현장 카메라 (C# WinForm)

| 기능 | 설명 |
|------|------|
| **YOLO 추론** | YOLOv8 모델 기반 원단 불량 실시간 검출 |
| **영상 송신** | 검출 결과 바인딩 박스·라벨이 포함된 영상을 Flask로 전송 |
| **검출 로그** | 불량 검출 이벤트 로그 기록 및 확인 |

### 서버 (Flask)

| 기능 | 설명 |
|------|------|
| **데이터 수집·저장** | MFC에서 전송한 센서 상태 데이터 수신 및 DB 저장 |
| **비상 정지 처리** | TCP/IP를 통한 비상 정지 명령 전송 |
| **웹 모니터링** | 생산라인 실시간 상태 확인 웹 대시보드 |
| **관리자 채팅** | 관리자 간 실시간 채팅 기능 |
| **통계 조회** | 불량 데이터 통계 및 이력 조회 |

### 하드웨어 제어 (Raspberry Pi)

| 기능 | 설명 |
|------|------|
| **센서 데이터 수집** | 온습도 센서, 적외선 센서 데이터 수집 및 MFC 전송 |
| **컨베이어 벨트 구동** | 모터 속도 제어 및 ON/OFF |
| **불량 분류 서보** | 불량 검출 시 서보모터를 통한 불량품 자동 분류 |
| **환풍기 제어** | 환풍기 모터 ON/OFF 제어 |
| **비상 정지 버튼** | 물리 비상 정지 버튼 입력 처리 |

---

## 🔄 시스템 구조

```
[Raspberry Pi]  ←─ RS-232 ─→  [MFC (C++)]  ←─ TCP/IP ─→  [Flask Server]  ←─ TCP/IP ─→  [WinForm (C#)]
 하드웨어 제어                   현장 관리자        ↕            데이터 처리                   현장 카메라
                                     │          HTTP                                      YOLO 추론
                                     └──────────→┘
```

### 통신 프로토콜 구조

| 구간 | 프로토콜 | 방향 | 용도 |
|------|----------|------|------|
| MFC ↔ Raspberry Pi | RS-232 Serial | 양방향 | 센서 데이터 수신 · 장비 제어 명령 송신 |
| MFC → Flask | HTTP | 단방향 | 센서 상태 데이터 전송 |
| Flask → MFC | TCP/IP | 단방향 | 비상 정지 명령 · YOLO 검출 영상 전달 |
| WinForm → Flask | HTTP | 단방향 | YOLO 검출 영상 업로드 |
| Flask → WinForm | TCP/IP | 단방향 | 비상 정지 명령 전달 |

### RS-232 패킷 구조 (9바이트)

```
[STX] [CMD] [LEN] [TEMP] [HUMI] [ULTR] [MOTR] [CS] [ETX]
 0x02  명령   길이   온도    습도   초음파   모터  체크섬  0x03
 1Byte 1Byte 1Byte 1Byte  1Byte  1Byte  1Byte 1Byte 1Byte
```

- **체크섬(CS)** : CMD ~ MOTR 필드의 XOR 연산으로 오류 검출
- **CMD 종류** : 상태 보고(0x10) · 컨베이어 제어(0x20) · 속도 설정(0x21) · 환풍기 제어(0x22) · 비상 정지(0x23) · 불량 서보(0x24)

### 데이터 흐름

> **센서 데이터** : Raspberry Pi → RS-232 → MFC → HTTP POST → Flask → DB 저장

> **불량 검출** : WinForm(YOLO 추론) → HTTP → Flask → TCP/IP → MFC → RS-232 → Raspberry Pi(서보 작동)

> **비상 정지** : Flask(웹) → TCP/IP → MFC → RS-232 → Raspberry Pi(모터 정지)

---

## 🎬 시연 영상

[![시연 영상](https://img.youtube.com/vi/fmbkl83N_QA/0.jpg)](https://youtu.be/fmbkl83N_QA)

> 클릭하면 영상으로 이동합니다.

---

## 🗄️ DB 구조

### MFC (SQLite)

```
alerts (알림 이력)
├── id          INTEGER PRIMARY KEY AUTOINCREMENT
├── timestamp   TEXT        -- "2025-05-27 14:32:11"
├── temperature REAL        -- 온도 값
├── humidity    REAL        -- 습도 값
├── status      TEXT        -- WARNING / DANGER / STOP / DEFECT
└── content     TEXT        -- 상세 설명
```

- **비동기 워커 스레드** : CRITICAL_SECTION + 이벤트 기반 큐로 UI 블로킹 없이 DB 저장
- **WAL 모드** : 동시 읽기/쓰기 성능 최적화
- **일별 요약 조회** : WARNING · DANGER · STOP 건수 집계

---

## 📁 프로젝트 구조 (MFC)

```
ConveyorSystem/
├── Conveyor_System.cpp/.h          # MFC 애플리케이션 진입점
├── Conveyor_SystemDlg.cpp/.h       # 메인 다이얼로그 (UI · 로직 통합)
├── ConveyorProtocol.h              # RS-232/TCP/HTTP 프로토콜 정의
├── SerialComm.cpp/.h               # RS-232 시리얼 통신 모듈
├── TcpClient.cpp/.h                # TCP 클라이언트 (Flask 연결)
├── TcpServer.cpp/.h                # TCP 서버
├── HttpClient.cpp/.h               # HTTP POST 클라이언트 (비동기 워커)
├── MonitorClient.cpp/.h            # YOLO 검출 영상 수신 및 렌더링
├── DatabaseManager.cpp/.h          # SQLite DB 관리 (비동기 큐)
├── ReportGenerator.cpp/.h          # PDF 보고서 생성
├── VoiceAlert.cpp/.h               # TTS 음성 알림
├── AlertHistoryDlg.cpp/.h          # 알림 이력 조회 다이얼로그
├── SettingsDlg.cpp/.h              # 환경 설정 다이얼로그
├── LoginDlg.cpp/.h                 # 로그인 다이얼로그
├── conveyor_settings.ini           # INI 설정 파일
└── resource.h / resource_additions.h  # 리소스 정의
```

---

## 🛠️ 기술 스택

| 분류 | 기술 |
|------|------|
| **MFC (현장 관리자)** | C++, MFC, Win32 API, GDI+, WinSock2, SQLite |
| **WinForm (현장 카메라)** | C#, WinForms, YOLOv8, OpenCV |
| **서버** | Python, Flask |
| **하드웨어** | Raspberry Pi, lgpio, 온습도 센서, 적외선 센서, 서보모터, DC모터 |
| **통신** | RS-232 Serial, TCP/IP Socket, HTTP |
| **데이터베이스** | SQLite (MFC), MySQL (Flask) |
| **동기화** | CRITICAL_SECTION, CreateThread, SetEvent |
| **IDE** | Visual Studio 2022, Visual Studio Code |

---

## 🔧 환경 설정

`conveyor_settings.ini` 파일로 시스템 설정을 관리합니다.

```ini
[Communication]
SerialPort=COM4
BaudRate=9600
FlaskIP=192.168.0.30
TcpPort=9000
HttpPort=5000

[Alert]
TempWarning=40.0
TempDanger=50.0
HumiWarning=50.0
HumiDanger=70.0

[Emergency]
CooldownSec=30
```

---

## 👥 팀 구성

| 이름 | 역할 | 담당 |
|------|------|------|
| 이준석 | 팀장 | UI 제작, 시스템 설계, 순서도 제작 |
| **김수영** | **부팀장** | **현장 관리자 기능 설계 및 제작, MFC 통신 모듈 설계, RS-232 통신 모듈 설계, 비상 정지 시스템 설계 및 제작, 전체 통신 시스템 설계, 현장 관리자 GUI 제작, SQLite 연동 및 설계** |
| 김동호 | 팀원 | YOLO 데이터 라벨링 및 학습, 불량 분류 하드웨어 제작, HSV 채널 변경 및 불량 분류 |
| 이승환 | 팀원 | 전체 회로 설계 및 제작, 라즈베리파이를 이용한 모터 및 센서 제어, RS-232 통신 모듈 설계 |
| 장효준 | 팀원 | C# 윈도우폼 기능 설계 및 제작, YOLOv8 사용하여 불량 분류, HSV 채널 변경 및 불량 분류 |
| 최지윤 | 팀원 | Flask 기능 설계 및 제작, Flask 통신 모듈 설계, 채팅 시스템 설계 및 제작, 비상 정지 시스템 설계 및 제작, 불량 검출 모니터링 설계 및 제작, 관리자 GUI 제작, MySQL 연동 및 설계 |
