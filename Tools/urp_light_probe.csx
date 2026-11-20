// MCP execute_code method body (Edit mode). Measures real Forward+ light contributions and
// verifies that approaching the surface does not drop overlapping lights. No saved assets change.
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var cameraObject = new GameObject("Forward+ light probe");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
var camera = cameraObject.AddComponent<Camera>();
camera.scene = scene;
camera.enabled = false;
camera.transform.position = new Vector3(0, 0, -3);
camera.orthographic = true;
camera.orthographicSize = 1;
camera.clearFlags = CameraClearFlags.SolidColor;
camera.backgroundColor = Color.black;
camera.allowHDR = true;
var data = UrpAccess.CameraData(camera);
UrpAccess.Set(data, "renderPostProcessing", false);
UrpAccess.Set(data, "volumeLayerMask", (LayerMask)0);
var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
camera.targetTexture = target;
var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(quad, scene);
quad.transform.localScale = Vector3.one * 4;
var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
material.SetColor("_BaseColor", Color.white);
material.SetFloat("_Metallic", 0);
material.SetFloat("_Smoothness", 0);
quad.GetComponent<Renderer>().sharedMaterial = material;
var lights = new System.Collections.Generic.List<Light>();
for (int i = 0; i < 6; i++)
{
    var go = new GameObject("Light " + i);
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    go.transform.position = new Vector3(Mathf.Sin(i * Mathf.PI / 3) * 0.6f, Mathf.Cos(i * Mathf.PI / 3) * 0.6f, -1);
    var light = go.AddComponent<Light>();
    light.type = LightType.Point; light.range = 5; light.intensity = 0.1f; light.enabled = false;
    lights.Add(light);
}
var pixels = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);
var previous = RenderTexture.active;
try
{
    System.Func<float> sample = () => {
        camera.Render(); RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); pixels.Apply();
        return pixels.GetPixel(32, 32).r;
    };
    float ambient = sample();
    lights[0].enabled = true;
    float one = sample();
    foreach (var light in lights) light.enabled = true;
    float six = sample();
    camera.transform.position = new Vector3(0, 0, -0.35f);
    float close = sample();
    float gain = (six - ambient) / Mathf.Max(0.00001f, one - ambient);
    bool passed = gain > 4.5f && Mathf.Abs(close - six) < 0.05f;
    return new { ambient, one, six, close, gain, passed };
}
finally
{
    RenderTexture.active = previous;
    camera.targetTexture = null;
    UnityEngine.Object.DestroyImmediate(pixels);
    UnityEngine.Object.DestroyImmediate(target);
    UnityEngine.Object.DestroyImmediate(material);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
