namespace WoWViewer.NET.Renderer
{
    public class ShaderEnums
    {
        public enum WMOVertexShader : int
        {
            None = -1,
            MapObjDiffuse_T1 = 0,
            MapObjDiffuse_T1_Refl = 1,
            MapObjDiffuse_T1_Env_T2 = 2,
            MapObjSpecular_T1 = 3,
            MapObjDiffuse_Comp = 4,
            MapObjDiffuse_Comp_Refl = 5,
            MapObjDiffuse_Comp_Terrain = 6,
            MapObjDiffuse_CompAlpha = 7,
            MapObjParallax = 8
        }

        public enum WMOPixelShader : int
        {
            None = -1,
            MapObjDiffuse = 0,
            MapObjSpecular = 1,
            MapObjMetal = 2,
            MapObjEnv = 3,
            MapObjOpaque = 4,
            MapObjEnvMetal = 5,
            MapObjTwoLayerDiffuse = 6,
            MapObjTwoLayerEnvMetal = 7,
            MapObjTwoLayerTerrain = 8,
            MapObjDiffuseEmissive = 9,
            MapObjMaskedEnvMetal = 10,
            MapObjEnvMetalEmissive = 11,
            MapObjTwoLayerDiffuseOpaque = 12,
            MapObjTwoLayerDiffuseEmissive = 13,
            MapObjAdditiveMaskedEnvMetal = 14,
            MapObjTwoLayerDiffuseMod2x = 15,
            MapObjTwoLayerDiffuseMod2xNA = 16,
            MapObjTwoLayerDiffuseAlpha = 17,
            MapObjLod = 18,
            MapObjParallax = 19,
            MapObjUnkShader = 20
        }

        public static List<(WMOVertexShader VertexShader, WMOPixelShader PixelShader)> WMOShaders =
        [
            (WMOVertexShader.MapObjDiffuse_T1, WMOPixelShader.MapObjDiffuse), // MapObjDiffuse 
            (WMOVertexShader.MapObjSpecular_T1, WMOPixelShader.MapObjSpecular), // MapObjSpecular
            (WMOVertexShader.MapObjSpecular_T1, WMOPixelShader.MapObjMetal), // MapObjMetal
            (WMOVertexShader.MapObjDiffuse_T1_Refl, WMOPixelShader.MapObjEnv), // MapObjEnv
            (WMOVertexShader.MapObjDiffuse_T1, WMOPixelShader.MapObjOpaque), // MapObjOpaque
            (WMOVertexShader.MapObjDiffuse_T1_Refl, WMOPixelShader.MapObjEnvMetal), // MapObjEnvMetal
            (WMOVertexShader.MapObjDiffuse_Comp, WMOPixelShader.MapObjTwoLayerDiffuse), // MapObjTwoLayerDiffuse
            (WMOVertexShader.MapObjDiffuse_T1, WMOPixelShader.MapObjTwoLayerEnvMetal), // MapObjTwoLayerEnvMetal
            (WMOVertexShader.MapObjDiffuse_Comp_Terrain, WMOPixelShader.MapObjTwoLayerTerrain), // MapObjTwoLayerTerrain
            (WMOVertexShader.MapObjDiffuse_Comp, WMOPixelShader.MapObjDiffuseEmissive), // MapObjDiffuseEmissive
            (WMOVertexShader.None, WMOPixelShader.None), // waterWindow
            (WMOVertexShader.MapObjDiffuse_T1_Env_T2, WMOPixelShader.MapObjMaskedEnvMetal), // MapObjMaskedEnvMetal
            (WMOVertexShader.MapObjDiffuse_T1_Env_T2, WMOPixelShader.MapObjEnvMetalEmissive), // MapObjEnvMetalEmissive
            (WMOVertexShader.MapObjDiffuse_Comp, WMOPixelShader.MapObjTwoLayerDiffuseOpaque), // MapObjTwoLayerDiffuseOpaque
            (WMOVertexShader.None, WMOPixelShader.None), // submarineWindow
            (WMOVertexShader.MapObjDiffuse_Comp, WMOPixelShader.MapObjTwoLayerDiffuseEmissive), // MapObjTwoLayerDiffuseEmissive
            (WMOVertexShader.MapObjDiffuse_T1, WMOPixelShader.MapObjDiffuse), // MapObjDiffuseTerrain
            (WMOVertexShader.MapObjDiffuse_T1_Env_T2, WMOPixelShader.MapObjAdditiveMaskedEnvMetal), // MapObjAdditiveMaskedEnvMetal
            (WMOVertexShader.MapObjDiffuse_CompAlpha, WMOPixelShader.MapObjTwoLayerDiffuseMod2x), // MapObjTwoLayerDiffuseMod2x
            (WMOVertexShader.MapObjDiffuse_Comp, WMOPixelShader.MapObjTwoLayerDiffuseMod2xNA), // MapObjTwoLayerDiffuseMod2xNA
            (WMOVertexShader.MapObjDiffuse_CompAlpha, WMOPixelShader.MapObjTwoLayerDiffuseAlpha), // MapObjTwoLayerDiffuseAlpha
            (WMOVertexShader.MapObjDiffuse_T1, WMOPixelShader.MapObjLod), // MapObjLod
            (WMOVertexShader.MapObjParallax, WMOPixelShader.MapObjParallax), // MapObjParallax
            (WMOVertexShader.MapObjDiffuse_T1, WMOPixelShader.MapObjUnkShader) // MapObjUnkShader
        ];
    }
}
