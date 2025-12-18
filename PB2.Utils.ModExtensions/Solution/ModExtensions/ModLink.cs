using PhantomBrigade.Mods;

namespace ModExtensions
{
    public class ModLinkCustom : ModLink
    {
        public static ModLinkCustom ins;
        
        public override void OnLoadStart()
        {
            ins = this;
            UnityEngine.Debug.Log ("ModExtensions | OnLoadStart");
        }
    }
}