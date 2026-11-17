#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Herramienta de editor para montar los managers del proyecto sin hacerlo a mano.
/// Menú: Drumixtry > ...
///
/// 1. "Montar Nivel 1": AudioManager (con clips y volúmenes) + prefab LevelSystems en la escena abierta.
/// 2. "Montar LevelSystems en escena actual": solo LevelManager + DifficultyManager (para Nivel 2 y 3).
///
/// Es seguro ejecutarlo varias veces: si el objeto ya existe en la escena, lo reutiliza.
/// </summary>
public static class ConfigurarManagers
{
    private const string CarpetaPrefabs = "Assets/Prefabs";
    private const string RutaPrefabLevelSystems = "Assets/Prefabs/LevelSystems.prefab";

    // Volúmenes moderados para que no saturen (ajustables luego en el Inspector).
    private const float VolumenMaestro = 0.8f;
    private const float VolumenAmbiente = 0.4f;
    private const float VolumenSFX = 0.7f;

    [MenuItem("Drumixtry/1. Montar Nivel 1 (AudioManager + LevelSystems)")]
    public static void MontarNivelUno()
    {
        var escena = EditorSceneManager.GetActiveScene();
        var informe = new List<string>();

        MontarAudioManager(informe);
        MontarLevelSystems(informe);
        AsegurarEscenaEnBuildSettings(escena.path, 0, informe);

        GuardarEscena(escena);
        Informar(informe);
    }

    /// <summary>Versión para línea de comandos (-batchmode -executeMethod): abre 1.NivelUno y lo configura.</summary>
    public static void MontarNivelUnoBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/1.NivelUno.unity", OpenSceneMode.Single);
        MontarNivelUno();
    }

    [MenuItem("Drumixtry/2. Montar LevelSystems en escena actual (Nivel 2 y 3)")]
    public static void MontarSoloLevelSystems()
    {
        var escena = EditorSceneManager.GetActiveScene();
        var informe = new List<string>();

        MontarLevelSystems(informe);

        GuardarEscena(escena);
        Informar(informe);
    }

    // ---------------------------------------------------------------
    // AudioManager
    // ---------------------------------------------------------------

    private static void MontarAudioManager(List<string> informe)
    {
        var audio = Object.FindFirstObjectByType<AudioManager>();
        if (audio == null)
        {
            var go = new GameObject("AudioManager");
            Undo.RegisterCreatedObjectUndo(go, "Crear AudioManager");
            audio = go.AddComponent<AudioManager>();
            informe.Add("AudioManager creado en la escena.");
        }
        else
        {
            informe.Add("AudioManager ya existía en la escena: se reconfiguró.");
        }

        var so = new SerializedObject(audio);

        AsignarTodosLosClips(so, informe);

        so.FindProperty("volumenMaestro").floatValue = VolumenMaestro;
        so.FindProperty("volumenAmbiente").floatValue = VolumenAmbiente;
        so.FindProperty("volumenSFX").floatValue = VolumenSFX;
        so.ApplyModifiedProperties();
        informe.Add($"Volúmenes: maestro {VolumenMaestro}, ambiente {VolumenAmbiente}, SFX {VolumenSFX}.");

        EditorUtility.SetDirty(audio);
    }

    /// <summary>
    /// Nombres de archivo que busca la herramienta para cada casilla del AudioManager.
    /// El PRIMER nombre es el definitivo; los siguientes son provisionales (audios que ya existen).
    /// </summary>
    private static void AsignarTodosLosClips(SerializedObject so, List<string> informe)
    {
        AsignarClip(so, "clipAmbiente", informe, "Ambiente_Concierto");
        AsignarClip(so, "sfxClicBoton", informe, "SFX_ClicBoton", "POP");
        AsignarClip(so, "sfxExito", informe, "SFX_Exito", "ExitoSound");
        AsignarClip(so, "sfxError", informe, "SFX_Error", "ErrorSound");
        AsignarClip(so, "sfxGanarNivel", informe, "SFX_GanarNivel", "ExitoSound");
        AsignarClip(so, "sfxPerderNivel", informe, "SFX_PerderNivel", "ErrorSound");
    }

    private static void AsignarClip(SerializedObject so, string campo, List<string> informe, params string[] nombres)
    {
        for (int i = 0; i < nombres.Length; i++)
        {
            var clip = BuscarClip(nombres[i]);
            if (clip == null) continue;

            so.FindProperty(campo).objectReferenceValue = clip;
            informe.Add($"{campo} = {clip.name}" + (i > 0 ? " (provisional)" : ""));
            return;
        }

        so.FindProperty(campo).objectReferenceValue = null;
        informe.Add($"{campo}: vacío (falta '{nombres[0]}').");
    }

    [MenuItem("Drumixtry/3. Asignar sonidos al AudioManager (por nombre)")]
    public static void AsignarSonidosPorNombre()
    {
        var audio = Object.FindFirstObjectByType<AudioManager>();
        if (audio == null)
        {
            EditorUtility.DisplayDialog("Drumixtry", "Abre la escena del Nivel 1: ahí está el AudioManager.", "OK");
            return;
        }

        var informe = new List<string>();
        var so = new SerializedObject(audio);
        AsignarTodosLosClips(so, informe);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(audio);

        GuardarEscena(EditorSceneManager.GetActiveScene());
        Informar(informe);
    }

    /// <summary>Versión de línea de comandos: abre el Nivel 1 y reasigna sonidos.</summary>
    public static void AsignarSonidosBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/1.NivelUno.unity", OpenSceneMode.Single);
        AsignarSonidosPorNombre();
    }

    // ---------------------------------------------------------------
    // Niveles 2 y 3 (copias del Nivel 1 con sus managers)
    // ---------------------------------------------------------------

    /// <summary>Crea 2.NivelDos y 3.NivelTres a partir de 1.NivelUno (si no existen) y los ordena en Build Settings.</summary>
    public static void CrearNivelesDosYTresBatch()
    {
        CrearNivelDesdeUno("Assets/Scenes/2.NivelDos.unity", 1);
        CrearNivelDesdeUno("Assets/Scenes/3.NivelTres.unity", 2);
        EditorSceneManager.OpenScene("Assets/Scenes/1.NivelUno.unity", OpenSceneMode.Single);
    }

    private static void CrearNivelDesdeUno(string rutaNueva, int indiceBuild)
    {
        const string origen = "Assets/Scenes/1.NivelUno.unity";
        var informe = new List<string>();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(rutaNueva) == null)
        {
            AssetDatabase.CopyAsset(origen, rutaNueva);
            informe.Add($"Escena creada: {rutaNueva}");
        }

        var escena = EditorSceneManager.OpenScene(rutaNueva, OpenSceneMode.Single);

        // El AudioManager vive solo en el Nivel 1 (persiste con DontDestroyOnLoad).
        foreach (var audio in Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None))
        {
            informe.Add("AudioManager duplicado eliminado de " + rutaNueva);
            Object.DestroyImmediate(audio.gameObject);
        }

        MontarLevelSystems(informe);
        AsegurarEscenaEnBuildSettings(rutaNueva, indiceBuild, informe);
        GuardarEscena(escena);
        Debug.Log("[ConfigurarManagers]\n" + string.Join("\n", informe));
    }

    private static AudioClip BuscarClip(string nombre)
    {
        foreach (var guid in AssetDatabase.FindAssets(nombre + " t:AudioClip"))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(ruta) == nombre)
                return AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
        }
        return null;
    }

    // ---------------------------------------------------------------
    // LevelManager + DifficultyManager (prefab)
    // ---------------------------------------------------------------

    private static void MontarLevelSystems(List<string> informe)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabLevelSystems);
        if (prefab == null)
            prefab = CrearPrefabLevelSystems(informe);

        var existente = Object.FindFirstObjectByType<LevelManager>();
        if (existente != null)
        {
            // La escena ya trae su propio LevelManager (ej. en "GameController"):
            // no se duplica; solo se añade el DifficultyManager al mismo objeto si falta.
            var difficulty = Object.FindFirstObjectByType<DifficultyManager>();
            if (difficulty == null)
            {
                difficulty = Undo.AddComponent<DifficultyManager>(existente.gameObject);
                var so = new SerializedObject(difficulty);
                so.FindProperty("levelManager").objectReferenceValue = existente;
                so.ApplyModifiedProperties();
                informe.Add($"DifficultyManager añadido al objeto '{existente.gameObject.name}' (que ya tenía LevelManager).");
            }
            else
            {
                informe.Add("La escena ya tiene LevelManager y DifficultyManager: no se cambió nada.");
            }
            return;
        }

        var instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instancia, "Añadir LevelSystems");
        informe.Add("Prefab LevelSystems añadido a la escena.");
    }

    private static GameObject CrearPrefabLevelSystems(List<string> informe)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaPrefabs))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var temporal = new GameObject("LevelSystems");
        var levelManager = temporal.AddComponent<LevelManager>();
        var difficulty = temporal.AddComponent<DifficultyManager>();

        var so = new SerializedObject(difficulty);
        so.FindProperty("levelManager").objectReferenceValue = levelManager;
        so.ApplyModifiedProperties();

        var prefab = PrefabUtility.SaveAsPrefabAsset(temporal, RutaPrefabLevelSystems);
        Object.DestroyImmediate(temporal);
        informe.Add($"Prefab creado: {RutaPrefabLevelSystems}");
        return prefab;
    }

    // ---------------------------------------------------------------
    // Build Settings
    // ---------------------------------------------------------------

    private static void AsegurarEscenaEnBuildSettings(string rutaEscena, int indiceDeseado, List<string> informe)
    {
        if (string.IsNullOrEmpty(rutaEscena)) return;

        var escenas = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int actual = escenas.FindIndex(s => s.path == rutaEscena);

        if (actual == indiceDeseado)
        {
            informe.Add($"Build Settings: '{rutaEscena}' ya está en el índice {indiceDeseado}.");
            return;
        }

        if (actual >= 0)
        {
            var existente = escenas[actual];
            escenas.RemoveAt(actual);
            escenas.Insert(indiceDeseado, existente);
        }
        else
        {
            escenas.Insert(indiceDeseado, new EditorBuildSettingsScene(rutaEscena, true));
        }

        EditorBuildSettings.scenes = escenas.ToArray();
        informe.Add($"Build Settings: '{rutaEscena}' colocada en el índice {indiceDeseado}.");
    }

    // ---------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------

    private static void GuardarEscena(UnityEngine.SceneManagement.Scene escena)
    {
        EditorSceneManager.MarkSceneDirty(escena);
        if (!string.IsNullOrEmpty(escena.path))
            EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
    }

    private static void Informar(List<string> informe)
    {
        var texto = string.Join("\n", informe);
        Debug.Log("[ConfigurarManagers]\n" + texto);
        EditorUtility.DisplayDialog("Drumixtry", texto, "OK");
    }
}
#endif
