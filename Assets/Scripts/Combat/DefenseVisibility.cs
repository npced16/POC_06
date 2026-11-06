namespace IronRing.Combat
{
    /// <summary>
    /// 스펙 006. 디펜스(정차역 전투·포탑·정비) 화면을 숨기는 스위치. 기능 코드는 그대로 두고 화면과 입력만 막는다.
    /// false로 바꾸면 전투 경유지, 마을 열차·포탑, 정비 창, 도감 포탑 탭, 포탑 해금 알림이 모두 원래대로 돌아온다.
    /// </summary>
    public static class DefenseVisibility
    {
        /// <summary>true면 디펜스 관련 화면을 숨긴다.</summary>
        public static bool Hidden => true;
    }
}
