// 표현 계층(탑뷰 2D 씬, 연출, HUD) 공용 계약. 코디네이터 소유: 워커는 구현하거나 사용만 하고 수정하지 않는다.
// 게임 규칙 계약(IronRingContracts.cs)은 Unity에 의존하지 않지만, 이 파일은 씬 객체를 다루므로 UnityEngine 타입을 쓴다.
//
// 누가 무엇을 구현하는가 (스토리 -> 계약):
//   009 탑뷰 메인 씬  -> IMapAnchors, IHubClickSource, GameServices 등록과 MarkReady 호출
//   010 상태·전투 연출 -> IMapAnchors를 읽어 거점/노드 위치에 연출을 붙인다 (GameServices.Ready 이후)
//   011 한국어 HUD     -> IHubClickSource를 구독해 패널을 연다 (GameServices.Ready 이후)
//
// GameServices는 병렬 작업을 위한 얇은 등록소다. 컴포지션 루트(Bootstrap)가 실제 시스템을 만든 뒤 등록하고
// MarkReady()를 부른다. 010/011은 [RuntimeInitializeOnLoadMethod]로 부팅한 뒤 Ready를 기다려 자기 객체를 만든다.
// 각 시스템의 핵심 로직은 여전히 생성자 주입으로 조립된다. 이 등록소는 표현 계층 연결에만 쓴다.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronRing.Contracts.Presentation
{
    /// <summary>스토리 009가 구현. 탑뷰 맵의 월드 위치를 노드/거점 id로 찾는다.</summary>
    public interface IMapAnchors
    {
        /// <summary>선로 노드(거점·경유지) id에 해당하는 월드 앵커. 없으면 null.</summary>
        Transform NodeAnchor(string nodeId);
        /// <summary>거점 id에 해당하는 월드 앵커(건물 중심). 없으면 null.</summary>
        Transform HubAnchor(string hubId);
        /// <summary>열차 스프라이트의 Transform.</summary>
        Transform TrainAnchor { get; }
        /// <summary>맵을 비추는 직교 카메라.</summary>
        Camera MapCamera { get; }
    }

    /// <summary>스토리 009가 구현. 플레이어가 거점 스프라이트를 클릭하면 발생한다.</summary>
    public interface IHubClickSource
    {
        /// <summary>클릭된 거점 id.</summary>
        event Action<string> HubClicked;
    }

    /// <summary>
    /// 이벤트 연출용 정보. Bootstrap이 실제 WaypointResolver를 감싸는 어댑터로 구현해 GameServices에 등록한다.
    /// 스토리 039: 전투가 사라져 휴식지 이벤트 결과만 알린다. IWaypointResolver의 EncounterStarted/EncounterEnded와 함께 쓴다.
    /// </summary>
    public interface IEncounterPresentationSource
    {
        /// <summary>이벤트가 끝날 때: 문자열 테이블 키.</summary>
        event Action<string> EventResolved;
    }

    /// <summary>
    /// 스토리 012가 구현. 지금 선로 화면인지 거점 내부 화면인지. 011 HUD는 InteriorOpen이면 거점 관리 패널을 숨긴다.
    /// </summary>
    public interface IViewMode
    {
        /// <summary>거점 내부 화면이 열려 있는지.</summary>
        bool InteriorOpen { get; }
        /// <summary>열려 있는 내부 화면의 거점 id, 아니면 null.</summary>
        string InteriorHubId { get; }
        /// <summary>내부 화면이 열리거나(true) 닫힐 때(false).</summary>
        event Action<bool> InteriorChanged;
    }

    /// <summary>스토리 012가 구현. 내부 화면을 프로그램으로 열고 닫는다(데모·테스트용). 정차 시 자동 열림은 그대로 유지한다.</summary>
    public interface IInteriorNavigation
    {
        /// <summary>해당 거점의 내부 화면을 연다. 이미 열려 있으면 무시.</summary>
        void Open(string hubId);
        /// <summary>내부 화면을 닫고 선로 화면으로 돌아간다.</summary>
        void Close();
    }

    /// <summary>스토리 012가 구현. 거점 내부 칸의 월드 위치와 내부 맵의 카메라 중심.</summary>
    public interface IInteriorAnchors
    {
        /// <summary>해당 거점 내부 칸의 월드 앵커(스프라이트를 붙일 자리). 내부 맵이 아직 없으면 null.</summary>
        Transform CellAnchor(string hubId, int cellIndex);
        /// <summary>내부 맵의 열차 앵커(NPC가 주변을 돌아다니는 기준).</summary>
        Transform TrainAnchor(string hubId);
        /// <summary>내부 맵 전체의 중심 위치(카메라 이동용).</summary>
        Vector3 Center(string hubId);
    }

    /// <summary>스토리 012가 구현. 내부 화면에서 플레이어가 칸을 클릭하면 발생한다. 013/014는 칸 상태를 보고 자기 메뉴를 연다.</summary>
    /// <summary>스토리 017. 내부 화면에서 NPC를 클릭했을 때(빈 손일 때만). 표현 계층이 등록하고 HUD가 구독한다.</summary>
    public interface INpcClickSource
    {
        /// <summary>hubId, npcId.</summary>
        event Action<string, string> NpcClicked;
    }

    public interface IInteriorCellClickSource
    {
        /// <summary>hubId, cellIndex.</summary>
        event Action<string, int> CellClicked;
    }

    /// <summary>표현 계층 연결용 등록소. Bootstrap이 채우고 MarkReady()를 부른다.</summary>
    public static class GameServices
    {
        static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// 병렬 개발용 조립 훅. Bootstrap이 핵심 일곱 시스템(과 012의 내부 시스템)을 등록한 뒤, MarkReady() 직전에
        /// RaiseComposing()을 부른다. 다른 스토리의 설치자는 [RuntimeInitializeOnLoadMethod(SubsystemRegistration)]에서
        /// 이 이벤트를 구독해 GameServices.Get으로 의존을 얻고 자기 서비스를 Register한다. Reset()이 구독을 지운다.
        /// </summary>
        public static event Action Composing;

        /// <summary>Bootstrap만 호출한다. 구독자가 서비스를 등록할 기회를 준다.</summary>
        public static void RaiseComposing()
        {
            Composing?.Invoke();
        }

        /// <summary>MarkReady()가 호출되었는지.</summary>
        public static bool IsReady { get; private set; }

        /// <summary>모든 서비스가 등록된 뒤 한 번 발생한다. 이미 Ready면 구독 즉시 호출된다.</summary>
        public static event Action Ready
        {
            add { _ready += value; if (IsReady) value?.Invoke(); }
            remove { _ready -= value; }
        }
        static Action _ready;

        /// <summary>서비스를 등록한다. 같은 타입을 다시 등록하면 덮어쓴다.</summary>
        public static void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        /// <summary>등록된 서비스를 돌려준다. 없으면 null.</summary>
        public static T Get<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var s) ? (T)s : null;
        }

        /// <summary>등록 완료를 알린다. Ready 구독자가 호출된다.</summary>
        public static void MarkReady()
        {
            IsReady = true;
            _ready?.Invoke();
        }

        /// <summary>새 런이나 테스트를 위해 모든 등록을 지운다.</summary>
        public static void Reset()
        {
            _services.Clear();
            IsReady = false;
            _ready = null;
            Composing = null;
        }
    }
}
