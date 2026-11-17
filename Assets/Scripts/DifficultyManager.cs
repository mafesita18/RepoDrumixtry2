using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Bloque de datos con los parámetros de dificultad de UN nivel.
/// Es una clase [Serializable], así que se edita directamente en el Inspector.
/// TODOS los números son valores genéricos de propuesta: ajustar tras la reunión de diseño.
/// </summary>
[Serializable]
public class DificultadNivel
{
    [Tooltip("Etiqueta solo informativa (ej. 'Nivel 1 - Fácil').")]
    public string nombre = "Nivel";

    [Header("Velocidad")]
    [Tooltip("Multiplicador de velocidad de los elementos (notas/objetos). 1 = velocidad base.")]
    [Min(0.1f)] public float velocidadElementos = 1f;

    [Header("Tiempo")]
    [Tooltip("Tiempo límite del nivel en segundos. 0 = sin límite de tiempo.")]
    [Min(0f)] public float tiempoLimiteSegundos = 90f;

    [Header("Precisión")]
    [Tooltip("Margen de error en segundos: ventana de tolerancia para acertar un golpe. Más pequeño = más difícil.")]
    [Min(0.01f)] public float margenErrorSegundos = 0.30f;

    [Tooltip("Cantidad de fallos permitidos antes de perder. 0 = sin límite de fallos.")]
    [Min(0)] public int fallosPermitidos = 10;
}

/// <summary>
/// Define la dificultad escalable por nivel y la expone al resto del juego.
/// Se comunica con <see cref="LevelManager"/> para saber en qué nivel está el jugador
/// (índice de escena) y para reiniciar el nivel cuando se acaba el tiempo.
///
/// USO:
/// 1. Colocar este componente (junto al LevelManager) en un GameObject de cada nivel.
/// 2. Ajustar la lista "niveles" en el Inspector (posición 0 = Nivel 1, 1 = Nivel 2, 2 = Nivel 3).
/// 3. Otros scripts leen <see cref="Actual"/>, <see cref="VelocidadElementos"/>, <see cref="MargenError"/>, etc.
/// 4. Opcional: llamar a <see cref="IniciarTemporizador"/> al empezar a jugar.
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("LevelManager de la escena. Si se deja vacío se busca automáticamente.")]
    [SerializeField] private LevelManager levelManager;

    [Header("Dificultad por nivel (índice 0 = Nivel 1)")]
    [Tooltip("VALORES POR DEFECTO GENÉRICOS: placeholders a definir con el equipo.")]
    [SerializeField] private DificultadNivel[] niveles = new DificultadNivel[]
    {
        new DificultadNivel { nombre = "Nivel 1 - Fácil",   velocidadElementos = 1.00f, tiempoLimiteSegundos = 90f, margenErrorSegundos = 0.30f, fallosPermitidos = 10 },
        new DificultadNivel { nombre = "Nivel 2 - Medio",   velocidadElementos = 1.25f, tiempoLimiteSegundos = 75f, margenErrorSegundos = 0.20f, fallosPermitidos = 7  },
        new DificultadNivel { nombre = "Nivel 3 - Difícil", velocidadElementos = 1.50f, tiempoLimiteSegundos = 60f, margenErrorSegundos = 0.12f, fallosPermitidos = 4  },
    };

    [Header("Temporizador")]
    [Tooltip("Si está activo, el temporizador arranca solo al iniciar la escena.")]
    [SerializeField] private bool iniciarTemporizadorAutomaticamente = false;

    [Tooltip("Si está activo, al agotarse el tiempo se llama a LevelManager.ReiniciarNivel().")]
    [SerializeField] private bool reiniciarNivelAlAgotarTiempo = true;

    [Header("Eventos")]
    [Tooltip("Se dispara cuando el tiempo límite llega a 0 (útil para mostrar UI de derrota, sonidos, etc.).")]
    public UnityEvent OnTiempoAgotado;

    private float tiempoRestante;
    private bool temporizadorActivo;

    // ---------------------------------------------------------------
    // Acceso a los datos del nivel actual
    // ---------------------------------------------------------------

    /// <summary>Datos de dificultad del nivel actual (según el índice de escena del LevelManager).</summary>
    public DificultadNivel Actual => ObtenerDificultad(levelManager != null ? levelManager.IndiceNivelActual : 0);

    /// <summary>Multiplicador de velocidad de los elementos del nivel actual.</summary>
    public float VelocidadElementos => Actual.velocidadElementos;

    /// <summary>Tiempo límite (segundos) del nivel actual. 0 = sin límite.</summary>
    public float TiempoLimite => Actual.tiempoLimiteSegundos;

    /// <summary>Margen de error (segundos) del nivel actual.</summary>
    public float MargenError => Actual.margenErrorSegundos;

    /// <summary>Fallos permitidos en el nivel actual. 0 = sin límite.</summary>
    public int FallosPermitidos => Actual.fallosPermitidos;

    /// <summary>Segundos que quedan en el temporizador.</summary>
    public float TiempoRestante => tiempoRestante;

    /// <summary>
    /// Devuelve la dificultad para un índice de escena concreto.
    /// Si el índice se sale de la lista, se usa el primer o el último elemento (clamp).
    /// </summary>
    public DificultadNivel ObtenerDificultad(int indiceNivel)
    {
        if (niveles == null || niveles.Length == 0)
        {
            Debug.LogWarning("[DifficultyManager] La lista de niveles está vacía. Se usan valores por defecto.");
            return new DificultadNivel();
        }

        return niveles[Mathf.Clamp(indiceNivel, 0, niveles.Length - 1)];
    }

    // ---------------------------------------------------------------
    // Temporizador
    // ---------------------------------------------------------------

    /// <summary>Inicia (o reinicia) la cuenta atrás con el tiempo límite del nivel actual.</summary>
    public void IniciarTemporizador()
    {
        tiempoRestante = TiempoLimite;
        temporizadorActivo = tiempoRestante > 0f; // 0 = sin límite: no hay cuenta atrás
    }

    /// <summary>Detiene la cuenta atrás (llamar, por ejemplo, al ganar el nivel).</summary>
    public void DetenerTemporizador()
    {
        temporizadorActivo = false;
    }

    // ---------------------------------------------------------------
    // Ciclo de vida de Unity
    // ---------------------------------------------------------------

    private void Awake()
    {
        if (levelManager == null)
            levelManager = FindAnyObjectByType<LevelManager>();

        if (levelManager == null)
            Debug.LogWarning("[DifficultyManager] No se encontró un LevelManager en la escena. Se usará el nivel 1.");
    }

    private void Start()
    {
        if (iniciarTemporizadorAutomaticamente)
            IniciarTemporizador();
    }

    private void Update()
    {
        if (!temporizadorActivo) return;

        tiempoRestante -= Time.deltaTime;
        if (tiempoRestante > 0f) return;

        tiempoRestante = 0f;
        temporizadorActivo = false;
        Debug.Log("[DifficultyManager] Tiempo agotado.");
        OnTiempoAgotado?.Invoke();

        if (reiniciarNivelAlAgotarTiempo && levelManager != null)
            levelManager.ReiniciarNivel();
    }
}
