public static partial class DariusPrototypeVfx
{
    public static GameObject CreateSummonerGhost(Transform owner, float duration)
    {
        if (owner == null) return null;
        GameObject aura = PersistentTexturedQuad("Darius_Ghost_Aura", "noxian_aura", owner, Vector3.up * 0.06f,
            new Vector3(1.85f,1f,1.85f),new Color(0.34f,0.86f,1.00f,0.62f),false,92f,0.08f,8.4f);
        TexturedQuad("Darius_Ghost_Burst", "q_heal_wisps", owner.position + Vector3.up * 0.18f,
            new Vector3(1.65f,1f,1.65f),Mathf.Min(0.48f, Mathf.Max(0.18f, duration * 0.12f)),
            new Color(0.52f,0.94f,1.00f,0.72f),false,owner,90f);
        DariusMedia.Play("ghost", owner.position, 0.90f);
        return aura;
    }
}