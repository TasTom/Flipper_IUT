var c=Camera.main;var old=c.targetTexture;var active=RenderTexture.active;
var rt=new RenderTexture(1920,1080,24);var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
try{c.targetTexture=rt;c.GetComponent<CabinetViewport>()?.Fit();c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();System.IO.File.WriteAllBytes("Tools/unity/out/reference-preview.png",tex.EncodeToPNG());}
finally{c.targetTexture=old;c.GetComponent<CabinetViewport>()?.Fit();RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
return "Tools/unity/out/reference-preview.png";
