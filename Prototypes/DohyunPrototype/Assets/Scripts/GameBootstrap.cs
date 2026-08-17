using DemonCompany.Phase1;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AdaptiveBossPrototype
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static Sprite sharedSprite;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BuildCamera();
            BuildEventSystem();
            GameObject controllerObject = new GameObject("Phase 1 Game Controller");
            Phase1GameController controller = controllerObject.AddComponent<Phase1GameController>();
            controller.Initialize();
        }

        private static void BuildEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventSystemObject = new GameObject("Event System");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static void BuildCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.065f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        public static GameObject CreateActor(string name, Vector2 position, Vector2 scale, Color color, float order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSharedSprite();
            renderer.color = color;
            renderer.sortingOrder = Mathf.RoundToInt(order * 10f);
            return obj;
        }

        private static Sprite GetSharedSprite()
        {
            if (sharedSprite != null) return sharedSprite;
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "Runtime White Pixel";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            sharedSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sharedSprite.name = "Runtime Square";
            return sharedSprite;
        }
    }
}
