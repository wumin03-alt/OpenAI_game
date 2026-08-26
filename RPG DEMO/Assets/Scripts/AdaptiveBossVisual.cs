using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 별도 아트 에셋 없이 기존 보스를 '학습 코어 + 데이터 노드' 형태로 보강합니다.
/// Phase 2와 데이터 교란 상태를 색/회전 속도로 즉시 읽을 수 있게 합니다.
/// </summary>
public class AdaptiveBossVisual : MonoBehaviour
{
    private readonly List<SpriteRenderer> nodes = new List<SpriteRenderer>();
    private Transform orbitRoot;
    private SpriteRenderer mainRenderer;
    private Sprite runtimeSprite;
    private float rotationSpeed = 28f;
    private bool initialized;
    private bool defeated;

    private readonly Color phase1Core = new Color(0.12f, 0.9f, 1f, 0.95f);
    private readonly Color phase1Node = new Color(0.15f, 0.55f, 0.9f, 0.9f);
    private readonly Color phase2Core = new Color(1f, 0.18f, 0.42f, 0.95f);
    private readonly Color phase2Node = new Color(0.72f, 0.12f, 1f, 0.9f);

    public void Initialize(SpriteRenderer bossRenderer)
    {
        if (initialized) return;
        initialized = true;
        mainRenderer = bossRenderer;

        // 최종 사이버 드래곤 아트에는 코어와 데이터 노드가 이미 포함되어 있습니다.
        // 프로토타입 사각형이 완성 스프라이트를 가리지 않도록 생성 단계를 생략합니다.
        if (mainRenderer != null && mainRenderer.sprite != null &&
            (mainRenderer.sprite.name.StartsWith("SPR_CyberDragon") || mainRenderer.sprite.rect.width > 16f))
            return;

        Texture2D pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        pixel.name = "RuntimeBossPixel";
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        runtimeSprite = Sprite.Create(pixel, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        runtimeSprite.name = "RuntimeBossSquare";

        orbitRoot = new GameObject("AdaptiveDataOrbit").transform;
        orbitRoot.SetParent(transform, false);
        orbitRoot.localPosition = new Vector3(0f, 1.15f, -0.05f);

        int sortingOrder = mainRenderer != null ? mainRenderer.sortingOrder + 1 : 2;
        CreateNode("Core", Vector2.zero, new Vector2(1.15f, 1.15f), phase1Core, sortingOrder + 1);

        const int count = 6;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector2 position = new Vector2(Mathf.Cos(angle) * 1.45f, Mathf.Sin(angle) * 0.8f);
            Vector2 size = i % 2 == 0 ? new Vector2(0.42f, 0.18f) : new Vector2(0.2f, 0.42f);
            CreateNode("DataNode_" + i, position, size, phase1Node, sortingOrder);
        }

        // 기존 본체 색은 Health의 피격 플래시와 충돌하지 않도록 유지합니다.
    }

    private void CreateNode(string objectName, Vector2 localPosition, Vector2 size, Color color, int order)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(orbitRoot, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = size;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = runtimeSprite;
        sr.color = color;
        sr.sortingOrder = order;
        nodes.Add(sr);
    }

    private void Update()
    {
        if (!initialized || defeated || orbitRoot == null) return;
        orbitRoot.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        float pulse = 0.92f + Mathf.Sin(Time.time * 4f) * 0.08f;
        if (nodes.Count > 0) nodes[0].transform.localScale = Vector3.one * (1.15f * pulse);
    }

    public void SetPhase(int phase)
    {
        if (!initialized) return;
        rotationSpeed = phase >= 2 ? -62f : 28f;
        Color core = phase >= 2 ? phase2Core : phase1Core;
        Color node = phase >= 2 ? phase2Node : phase1Node;

        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] != null) nodes[i].color = i == 0 ? core : node;
    }

    public void PulseDisruption()
    {
        if (isActiveAndEnabled) StartCoroutine(DisruptionRoutine());
    }

    private IEnumerator DisruptionRoutine()
    {
        Color[] previous = new Color[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null) continue;
            previous[i] = nodes[i].color;
            nodes[i].color = Color.white;
        }

        yield return new WaitForSecondsRealtime(0.12f);

        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] != null) nodes[i].color = previous[i];
    }

    public void SetDefeated()
    {
        defeated = true;
        rotationSpeed = 0f;
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] != null) nodes[i].color = new Color(0.25f, 0.27f, 0.32f, 0.75f);
    }

    private void OnDestroy()
    {
        if (runtimeSprite == null) return;
        Texture2D texture = runtimeSprite.texture;
        Destroy(runtimeSprite);
        if (texture != null) Destroy(texture);
    }
}
