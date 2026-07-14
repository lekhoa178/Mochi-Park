namespace MochiPark.Runtime.Board
{
    public readonly struct InfluenceLink
    {
        public InfluenceLink(string unitInstanceId, string areaId)
        {
            UnitInstanceId = unitInstanceId;
            AreaId = string.IsNullOrWhiteSpace(areaId) ? "main" : areaId;
        }

        public string UnitInstanceId { get; }
        public string AreaId { get; }
    }
}
