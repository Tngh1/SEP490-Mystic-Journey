using UnityEngine.SceneManagement;
using UnityEngine;

// Executes mono behaviour operation.
public class SceneTransition3 : MonoBehaviour
{
    public string sceneToLoad;
    public Vector2 playerPosition3;
    public VectorValue3 playerStorage;

    // Executes on trigger enter2 d operation.
    public void OnTriggerEnter2D(Collider2D other)
    {
        if(other.CompareTag("Player") && !other.isTrigger)
        {
            playerStorage.initialValue3 = playerPosition3;
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}
