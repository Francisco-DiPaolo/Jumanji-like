using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class SetupPlayerUI
{
    [MenuItem("Tools/Instalar UI en Player")]
    public static void SetupUI()
    {
        string prefabPath = "Assets/1 Game/Player/Prefab/Player.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        if (prefabRoot == null)
        {
            Debug.LogError("No se pudo cargar el prefab del jugador en: " + prefabPath);
            return;
        }

        // 1. Encontrar o crear Canvas
        Canvas canvas = prefabRoot.GetComponentInChildren<Canvas>();
        GameObject canvasObj;
        if (canvas == null)
        {
            canvasObj = new GameObject("PlayerCanvas");
            canvasObj.transform.SetParent(prefabRoot.transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvasObj = canvas.gameObject;
        }

        // 2. Crear GameUI (Contenedor principal)
        Transform existingUI = canvasObj.transform.Find("GameUI");
        GameObject uiManagerObj;
        GameUIManager uiManager;

        if (existingUI != null)
        {
            uiManagerObj = existingUI.gameObject;
            uiManager = uiManagerObj.GetComponent<GameUIManager>();
            if (uiManager == null) uiManager = uiManagerObj.AddComponent<GameUIManager>();
        }
        else
        {
            uiManagerObj = new GameObject("GameUI");
            uiManagerObj.transform.SetParent(canvasObj.transform, false);
            
            RectTransform uiRect = uiManagerObj.AddComponent<RectTransform>();
            uiRect.anchorMin = Vector2.zero;
            uiRect.anchorMax = Vector2.one;
            uiRect.sizeDelta = Vector2.zero;
            uiRect.anchoredPosition = Vector2.zero;
            
            uiManager = uiManagerObj.AddComponent<GameUIManager>();
        }

        // 3. Crear contenedor Abajo a la Izquierda
        GameObject bottomLeftContainer = new GameObject("StatusContainer");
        bottomLeftContainer.transform.SetParent(uiManagerObj.transform, false);
        RectTransform statusRect = bottomLeftContainer.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0); // Bottom Left
        statusRect.anchorMax = new Vector2(0, 0);
        statusRect.pivot = new Vector2(0, 0);
        statusRect.anchoredPosition = new Vector2(50, 50); // Offset de la esquina
        statusRect.sizeDelta = new Vector2(400, 150);

        // --- VIDA ---
        // Fondo Vida
        GameObject healthBgObj = new GameObject("BackgroundBar_White_1 (Health)");
        healthBgObj.transform.SetParent(statusRect.transform, false);
        RectTransform hBgRect = healthBgObj.AddComponent<RectTransform>();
        hBgRect.anchorMin = new Vector2(0, 1);
        hBgRect.anchorMax = new Vector2(0, 1);
        hBgRect.pivot = new Vector2(0, 1);
        hBgRect.anchoredPosition = new Vector2(0, 0);
        hBgRect.sizeDelta = new Vector2(300, 40);
        Image healthBg = healthBgObj.AddComponent<Image>();
        healthBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Relleno Vida
        GameObject healthFillObj = new GameObject("Bar_White (Health Fill)");
        healthFillObj.transform.SetParent(healthBgObj.transform, false);
        RectTransform hFillRect = healthFillObj.AddComponent<RectTransform>();
        hFillRect.anchorMin = Vector2.zero;
        hFillRect.anchorMax = Vector2.one;
        hFillRect.sizeDelta = new Vector2(-10, -10); // Margen interno
        hFillRect.anchoredPosition = Vector2.zero;
        
        Image healthFill = healthFillObj.AddComponent<Image>();
        healthFill.color = new Color(0.8f, 0.2f, 0.2f, 1f); // Rojo
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillAmount = 1f;

        // --- ESTAMINA ---
        // Contenedor Estamina (Icono + Barra)
        GameObject staminaContainer = new GameObject("StaminaGroup");
        staminaContainer.transform.SetParent(statusRect.transform, false);
        RectTransform sContRect = staminaContainer.AddComponent<RectTransform>();
        sContRect.anchorMin = new Vector2(0, 1);
        sContRect.anchorMax = new Vector2(0, 1);
        sContRect.pivot = new Vector2(0, 1);
        sContRect.anchoredPosition = new Vector2(0, -60); // Debajo de la vida
        sContRect.sizeDelta = new Vector2(300, 30);

        // Icono Estamina
        GameObject staminaIconObj = new GameObject("Stamina_White (Icon)");
        staminaIconObj.transform.SetParent(sContRect.transform, false);
        RectTransform sIconRect = staminaIconObj.AddComponent<RectTransform>();
        sIconRect.anchorMin = new Vector2(0, 0.5f);
        sIconRect.anchorMax = new Vector2(0, 0.5f);
        sIconRect.pivot = new Vector2(0, 0.5f);
        sIconRect.anchoredPosition = new Vector2(15, 0);
        sIconRect.sizeDelta = new Vector2(25, 25);
        Image staminaIcon = staminaIconObj.AddComponent<Image>();
        staminaIcon.color = Color.yellow; // Amarillo

        // Fondo Estamina
        GameObject staminaBgObj = new GameObject("BackgroundBar_White_1 (Stamina)");
        staminaBgObj.transform.SetParent(sContRect.transform, false);
        RectTransform sBgRect = staminaBgObj.AddComponent<RectTransform>();
        sBgRect.anchorMin = new Vector2(0, 0.5f);
        sBgRect.anchorMax = new Vector2(0, 0.5f);
        sBgRect.pivot = new Vector2(0, 0.5f);
        sBgRect.anchoredPosition = new Vector2(40, 0); // A la derecha del icono
        sBgRect.sizeDelta = new Vector2(200, 20);
        Image staminaBg = staminaBgObj.AddComponent<Image>();
        staminaBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Relleno Estamina
        GameObject staminaFillObj = new GameObject("Bar_White (Stamina Fill)");
        staminaFillObj.transform.SetParent(staminaBgObj.transform, false);
        RectTransform sFillRect = staminaFillObj.AddComponent<RectTransform>();
        sFillRect.anchorMin = Vector2.zero;
        sFillRect.anchorMax = Vector2.one;
        sFillRect.sizeDelta = new Vector2(-6, -6); // Margen
        sFillRect.anchoredPosition = Vector2.zero;
        
        Image staminaFill = staminaFillObj.AddComponent<Image>();
        staminaFill.color = new Color(0.2f, 0.8f, 0.2f, 1f); // Verde
        staminaFill.type = Image.Type.Filled;
        staminaFill.fillMethod = Image.FillMethod.Horizontal;
        staminaFill.fillAmount = 1f;

        // 4. Panel de Game Over
        GameObject gameOverObj = new GameObject("GameOverPanel");
        gameOverObj.transform.SetParent(uiManagerObj.transform, false);
        RectTransform goRect = gameOverObj.AddComponent<RectTransform>();
        goRect.anchorMin = Vector2.zero;
        goRect.anchorMax = Vector2.one;
        goRect.sizeDelta = Vector2.zero;
        goRect.anchoredPosition = Vector2.zero;
        Image goBg = gameOverObj.AddComponent<Image>();
        goBg.color = new Color(0, 0, 0, 0.8f); // Fondo oscuro
        gameOverObj.SetActive(false); // Oculto por defecto

        GameObject goTextObj = new GameObject("Text");
        goTextObj.transform.SetParent(gameOverObj.transform, false);
        RectTransform goTextRect = goTextObj.AddComponent<RectTransform>();
        goTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        goTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        goTextRect.sizeDelta = new Vector2(600, 100);
        goTextRect.anchoredPosition = Vector2.zero;
        
        // Asignar referencias por reflection ya que los campos son privados
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        uiManager.GetType().GetField("healthBarFill", flags).SetValue(uiManager, healthFill);
        uiManager.GetType().GetField("staminaBarFill", flags).SetValue(uiManager, staminaFill);
        uiManager.GetType().GetField("gameOverPanel", flags).SetValue(uiManager, gameOverObj);

        // 5. Guardar Prefab
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log("¡UI instalada correctamente en el prefab del Player!");
    }
}
