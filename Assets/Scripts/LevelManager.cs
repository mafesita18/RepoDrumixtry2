using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestiona el paso automático entre niveles usando el índice de la escena actual
/// (Build Settings): Nivel 1 -> Nivel 2 -> Nivel 3.
/// Orden requerido en File > Build Profiles (Build Settings): Nivel 1 = 0, Nivel 2 = 1, Nivel 3 = 2.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Índice (en Build Settings) del último nivel del juego. Nivel 3 = 2.")]
    [SerializeField] private int indiceUltimoNivel = 2;

    [Tooltip("Si es true, al ganar el último nivel se vuelve al primero. Si es false, solo se avisa en consola.")]
    [SerializeField] private bool volverAlInicioAlTerminar = false;

    /// <summary>Índice (Build Settings) del nivel actual. Lo consulta el DifficultyManager.</summary>
    public int IndiceNivelActual => SceneManager.GetActiveScene().buildIndex;

    /// <summary>Llamar cuando el jugador gana: carga el siguiente nivel según el índice actual.</summary>
    public void CargarSiguienteNivel()
    {
        int indiceActual = SceneManager.GetActiveScene().buildIndex;
        int siguiente = indiceActual + 1;

        if (indiceActual >= indiceUltimoNivel || siguiente >= SceneManager.sceneCountInBuildSettings)
        {
            if (volverAlInicioAlTerminar)
            {
                Debug.Log("[LevelManager] Juego completado. Volviendo al Nivel 1.");
                SceneManager.LoadScene(0);
            }
            else
            {
                Debug.Log("[LevelManager] ¡Último nivel completado! No hay más niveles.");
            }
            return;
        }

        Debug.Log($"[LevelManager] Cargando nivel con índice {siguiente}.");
        SceneManager.LoadScene(siguiente);
    }

    /// <summary>Reinicia el nivel (escena) actual.</summary>
    public void ReiniciarNivel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
