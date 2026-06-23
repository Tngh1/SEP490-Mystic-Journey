using UnityEngine.SceneManagement;
using UnityEngine;

public class SceneTransition3 : MonoBehaviour
{
    public string sceneToLoad;
    public Vector2 playerPosition3;
    public VectorValue3 playerStorage;
   
    public void OnTriggerEnter2D(Collider2D other)
    {
        if(other.CompareTag("Player") && !other.isTrigger)
        {
            playerStorage.initialValue3 = playerPosition3;
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}
