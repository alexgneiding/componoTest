using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;


public class ExportRoutine : MonoBehaviour
{   
    [SerializeField] private SpawnItemList m_itemList = null;

    private AssetReferenceGameObject m_assetLoadedAsset;
    private GameObject m_instanceObject = null;
    public Transform center;

    // Flag
    private bool modelComplete;

    private void Start()
    {
        if (m_itemList == null || m_itemList.AssetReferenceCount == 0) 
        {
            Debug.LogError("Spawn list not setup correctly");
        }
        StartCoroutine("IterateThroughModelsAndTakePhotos");
    }

    private void LoadItemAtIndex(SpawnItemList itemList, int index) 
    {
        if (m_instanceObject != null) 
        {
            Destroy(m_instanceObject);
        }
        
        m_assetLoadedAsset = itemList.GetAssetReferenceAtIndex(index);
        var spawnPosition = new Vector3();
        var spawnRotation = Quaternion.identity;
        var parentTransform = this.transform;

   

        var loadRoutine = m_assetLoadedAsset.LoadAssetAsync();
        loadRoutine.Completed += LoadRoutine_Completed;

        void LoadRoutine_Completed(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> obj)
        {
            m_instanceObject = Instantiate(obj.Result, spawnPosition, spawnRotation, parentTransform);
            Addressables.Release(obj);
            StartCoroutine("CameraSequence");
        }
    }

    /// <summary>
    /// Coroutine that iterates through models and starts camera sequence 
    /// coroutine for each model. Quits the application when it's done
    /// </summary>
    /// <returns></returns>
    private IEnumerator IterateThroughModelsAndTakePhotos()
    {
        for (int i = 0; i < m_itemList.AssetReferenceCount; i++)
        {
            LoadItemAtIndex(m_itemList, i);
            modelComplete = false;
            yield return new WaitUntil(()=>modelComplete);
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else

        Application.Quit();
#endif
        yield break;
    }

    /// <summary>
    /// Camera coroutine that iterates through each angle and takes a photo
    /// and saves to device
    /// </summary>
    /// <returns></returns>
    private IEnumerator CameraSequence()
    {
        int currentY = 0;
        int steps = 16;
       
        var directory = GetOutputDirectory();
        SetCameraPositionBasedOnObjectToCapture(Camera.main, m_instanceObject);
        for (int i = 0; i < steps; i++)
        {
            yield return new WaitForEndOfFrame();
            center.eulerAngles = new Vector3(0, currentY, 0);
            yield return new WaitForEndOfFrame();
            //Create RT and load into Camera
            RenderTexture rt = new RenderTexture(512, 512, 24);
            Camera.main.targetTexture = rt;
            
            //Create texture and reads camera data into it and encodes to png
            Texture2D screenShot = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            screenShot.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            screenShot.Apply();
            var screenShotByteArray = screenShot.EncodeToPNG();
            
            
            //Create new thread for saving each file
            new System.Threading.Thread(() =>
            {
                File.WriteAllBytes(directory + "/frame" + i.ToString("0000") + ".png", screenShotByteArray);
                
            }).Start();

            
            //Clean
            Camera.main.targetTexture = null;
            Destroy(rt);
            screenShot = null;

            currentY += 360 / steps;
            yield return null;
            
        }

        //Clean m_instanced Object and set flag
        Destroy(m_instanceObject);
        m_instanceObject = null;
        modelComplete = true;

        yield break;
    }

    /// <summary>
    /// Iterates through each renderer and finds the biggest renderer.
    /// Bases the camera view on this biggest renderer
    /// TODO: fix for objects that don't have renderers that cover the entire object
    /// </summary>
    /// <param name="cam"></param>
    /// <param name="targetObject"></param>
    private void SetCameraPositionBasedOnObjectToCapture(Camera cam, GameObject targetObject)
    {
      
        //centers Object
        center.position = GetBiggestRenderer().bounds.center;

        //sets size of camera + 10% padding
        cam.orthographicSize = GetHighestSideValue() / 2f * 1.1f;

      


        Renderer GetBiggestRenderer()
        {
            float volume = 0;
            Renderer biggestRenderer = null;
            foreach (Renderer r in targetObject.GetComponentsInChildren<Renderer>())
            {
                var volumeTest = r.bounds.size.x * r.bounds.size.y * r.bounds.size.z;
                if (volume < volumeTest)
                {
                    biggestRenderer = r;
                    volume = volumeTest;
                }
                    
                
            }
            return biggestRenderer;
        }


        float GetHighestSideValue()
        {
            float biggestSideValue = 0;
            
            foreach(Renderer r in targetObject.GetComponentsInChildren<Renderer>())
            {
                
                if (biggestSideValue < r.bounds.size.x)
                    biggestSideValue = r.bounds.size.x;

                if (biggestSideValue < r.bounds.size.y)
                    biggestSideValue = r.bounds.size.y;

                if (biggestSideValue < r.bounds.size.z)
                    biggestSideValue = r.bounds.size.z;
            }

            Debug.Log(biggestSideValue);
            return biggestSideValue;
        }
    }

    /// <summary>
    /// Gets output directory. Creates directory if none exist
    /// </summary>
    /// <returns></returns>
    private string GetOutputDirectory()
    {
        var directory = Directory.GetCurrentDirectory() + "/output/"+m_instanceObject.name.Replace("(Clone)","");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return directory;
    }
}
