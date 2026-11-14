using UnityEngine;

/// <summary>
/// Gestor de audio global (Singleton) que persiste entre escenas con DontDestroyOnLoad.
///
/// - Usa DOS AudioSource separados: uno para el ambiente/música general (en loop)
///   y otro para los SFX de la interfaz.
/// - Los clips se asignan desde el Inspector (quedan vacíos hasta que se agreguen los audios).
/// - El volumen maestro y el de cada categoría se ajustan por código (y se guardan en PlayerPrefs).
///
/// USO:
/// 1. Colocar este componente en un GameObject de la PRIMERA escena del juego (el Nivel 1).
///    Si otra escena trae otro AudioManager, el duplicado se destruye solo.
/// 2. Asignar los clips en el Inspector cuando estén listos.
/// 3. Desde otros scripts: AudioManager.Instance.ReproducirSFXExito();
///                         AudioManager.Instance.SetVolumenAmbiente(0.5f);
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ---------------------------------------------------------------
    // Singleton
    // ---------------------------------------------------------------

    /// <summary>Instancia única accesible desde cualquier script.</summary>
    public static AudioManager Instance { get; private set; }

    // ---------------------------------------------------------------
    // Fuentes de audio (separadas por categoría)
    // ---------------------------------------------------------------

    [Header("Fuentes de audio (se crean solas si se dejan vacías)")]
    [Tooltip("AudioSource para el ambiente/música general. Se reproduce en loop.")]
    [SerializeField] private AudioSource fuenteAmbiente;

    [Tooltip("AudioSource para los efectos de sonido (SFX) de la interfaz.")]
    [SerializeField] private AudioSource fuenteSFX;

    // ---------------------------------------------------------------
    // Clips (a asignar luego en el Inspector)
    // ---------------------------------------------------------------

    [Header("Ambiente")]
    [Tooltip("Sonido/música de ambiente general. Se reproduce en loop.")]
    [SerializeField] private AudioClip clipAmbiente;

    [Tooltip("Reproducir el ambiente automáticamente al iniciar el juego.")]
    [SerializeField] private bool reproducirAmbienteAlIniciar = true;

    [Header("SFX de interfaz")]
    [SerializeField] private AudioClip sfxClicBoton;
    [SerializeField] private AudioClip sfxExito;
    [SerializeField] private AudioClip sfxError;
    [SerializeField] private AudioClip sfxGanarNivel;
    [SerializeField] private AudioClip sfxPerderNivel;

    // ---------------------------------------------------------------
    // Volúmenes (0 a 1)
    // ---------------------------------------------------------------

    [Header("Volúmenes (0 - 1)")]
    [Range(0f, 1f)] [SerializeField] private float volumenMaestro = 1f;
    [Range(0f, 1f)] [SerializeField] private float volumenAmbiente = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float volumenSFX = 1f;

    [Tooltip("Guardar y cargar los volúmenes con PlayerPrefs entre sesiones.")]
    [SerializeField] private bool guardarVolumenes = true;

    private const string ClaveMaestro = "Audio_Maestro";
    private const string ClaveAmbiente = "Audio_Ambiente";
    private const string ClaveSFX = "Audio_SFX";

    /// <summary>Volumen maestro actual (0 a 1).</summary>
    public float VolumenMaestro => volumenMaestro;
    /// <summary>Volumen de la categoría ambiente (0 a 1).</summary>
    public float VolumenAmbiente => volumenAmbiente;
    /// <summary>Volumen de la categoría SFX (0 a 1).</summary>
    public float VolumenSFX => volumenSFX;

    // ---------------------------------------------------------------
    // Ciclo de vida
    // ---------------------------------------------------------------

    private void Awake()
    {
        // Singleton: si ya existe otra instancia, esta se destruye.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // persiste al cambiar de nivel

        CrearFuentesSiFaltan();

        if (guardarVolumenes)
            CargarVolumenes();

        AplicarVolumenes();
    }

    private void Start()
    {
        if (Instance == this && reproducirAmbienteAlIniciar)
            ReproducirAmbiente();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    // Permite probar los sliders del Inspector en tiempo real.
    private void OnValidate()
    {
        if (Application.isPlaying && Instance == this)
            AplicarVolumenes();
    }
#endif

    // ---------------------------------------------------------------
    // Ambiente
    // ---------------------------------------------------------------

    /// <summary>Reproduce en loop el clip de ambiente asignado en el Inspector.</summary>
    public void ReproducirAmbiente()
    {
        ReproducirAmbiente(clipAmbiente);
    }

    /// <summary>Reproduce en loop un clip de ambiente concreto (por ejemplo, uno distinto por nivel).</summary>
    public void ReproducirAmbiente(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.Log("[AudioManager] No hay clip de ambiente asignado todavía.");
            return;
        }

        // Si ya está sonando ese mismo clip, no se reinicia.
        if (fuenteAmbiente.clip == clip && fuenteAmbiente.isPlaying) return;

        fuenteAmbiente.clip = clip;
        fuenteAmbiente.loop = true;
        fuenteAmbiente.Play();
    }

    /// <summary>Detiene el ambiente.</summary>
    public void DetenerAmbiente()
    {
        fuenteAmbiente.Stop();
    }

    // ---------------------------------------------------------------
    // SFX de interfaz
    // ---------------------------------------------------------------

    /// <summary>Reproduce un SFX cualquiera una vez (sin cortar otros SFX).</summary>
    public void ReproducirSFX(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.Log("[AudioManager] SFX sin clip asignado todavía; no se reproduce.");
            return;
        }

        // PlayOneShot permite solapar sonidos; el volumen de la fuente se aplica automáticamente.
        fuenteSFX.PlayOneShot(clip);
    }

    // Atajos para los SFX predefinidos (también sirven para enlazar a botones/UnityEvents).
    public void ReproducirSFXClicBoton() => ReproducirSFX(sfxClicBoton);
    public void ReproducirSFXExito() => ReproducirSFX(sfxExito);
    public void ReproducirSFXError() => ReproducirSFX(sfxError);
    public void ReproducirSFXGanarNivel() => ReproducirSFX(sfxGanarNivel);
    public void ReproducirSFXPerderNivel() => ReproducirSFX(sfxPerderNivel);

    // ---------------------------------------------------------------
    // Control de volumen por código
    // ---------------------------------------------------------------

    /// <summary>Ajusta el volumen maestro (afecta a todas las categorías). Valor 0 a 1.</summary>
    public void SetVolumenMaestro(float valor)
    {
        volumenMaestro = Mathf.Clamp01(valor);
        AplicarVolumenes();
        GuardarVolumenes();
    }

    /// <summary>Ajusta el volumen de la categoría ambiente. Valor 0 a 1.</summary>
    public void SetVolumenAmbiente(float valor)
    {
        volumenAmbiente = Mathf.Clamp01(valor);
        AplicarVolumenes();
        GuardarVolumenes();
    }

    /// <summary>Ajusta el volumen de la categoría SFX de interfaz. Valor 0 a 1.</summary>
    public void SetVolumenSFX(float valor)
    {
        volumenSFX = Mathf.Clamp01(valor);
        AplicarVolumenes();
        GuardarVolumenes();
    }

    // ---------------------------------------------------------------
    // Internos
    // ---------------------------------------------------------------

    /// <summary>Volumen final de cada fuente = volumen de categoría x volumen maestro.</summary>
    private void AplicarVolumenes()
    {
        if (fuenteAmbiente != null) fuenteAmbiente.volume = volumenAmbiente * volumenMaestro;
        if (fuenteSFX != null) fuenteSFX.volume = volumenSFX * volumenMaestro;
    }

    private void CrearFuentesSiFaltan()
    {
        if (fuenteAmbiente == null)
            fuenteAmbiente = gameObject.AddComponent<AudioSource>();
        if (fuenteSFX == null)
            fuenteSFX = gameObject.AddComponent<AudioSource>();

        fuenteAmbiente.playOnAwake = false;
        fuenteAmbiente.loop = true;
        fuenteSFX.playOnAwake = false;
        fuenteSFX.loop = false;
    }

    private void CargarVolumenes()
    {
        volumenMaestro = PlayerPrefs.GetFloat(ClaveMaestro, volumenMaestro);
        volumenAmbiente = PlayerPrefs.GetFloat(ClaveAmbiente, volumenAmbiente);
        volumenSFX = PlayerPrefs.GetFloat(ClaveSFX, volumenSFX);
    }

    private void GuardarVolumenes()
    {
        if (!guardarVolumenes) return;

        PlayerPrefs.SetFloat(ClaveMaestro, volumenMaestro);
        PlayerPrefs.SetFloat(ClaveAmbiente, volumenAmbiente);
        PlayerPrefs.SetFloat(ClaveSFX, volumenSFX);
        PlayerPrefs.Save();
    }
}
