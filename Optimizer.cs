/*
*
MIT License

Copyright (c) 2017 Sanyam Malhotra

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*
*/
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Ex = System.InvalidCastException;
using NotSupported = System.NotSupportedException;
using System.IO;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Reflection;

public class Optimizer : EditorWindow
{
    static List<Texture2D> m_Textures;
    static List<Material> m_Materials;
    static List<Sprite> m_Sprites;
    static List<AudioClip> m_AudioClips;
    static List<Mesh> m_Meshes;
    static List<GameObject> m_PrefabObjects;
    static List<GameObject> m_SceneGameObjects;

    static List<Material> m_DuplicateMaterials;
    static List<Texture2D> m_DuplicateTextures;
    static List<Sprite> m_DuplicateSprites;
    static List<AudioClip> m_DuplicateAudioClips;
    static List<Mesh> m_DuplicateMeshes;

    static Dictionary<Texture2D, string> textureHashDictionary;
    static Dictionary<Texture2D, Texture2D> m_TextureDuplicacyDictionary;
    static Dictionary<Material, Material> m_MaterialDuplicacyDictionary;
    static Dictionary<Sprite, Sprite> m_SpriteDuplicacyDictionary;
    static Dictionary<AudioClip, AudioClip> m_AudioClipDuplicacyDictionary;
    static Dictionary<Mesh, Mesh> m_MeshDuplicacyDictionary;

    static float r = 0, g = 0, b = 0;

    static string m_PathForDuplicates = "Assets";

    [MenuItem("State-of-the-Art Useless Tools/Optimizer")]
    public static void ShowWindow()
    {
        GetWindow(typeof(Optimizer));

    }

    void OnGUI()
    {

        if (GUILayout.Button("Run Program"))
        {
            System.DateTime start = System.DateTime.Now;
            RunApplication();
            System.DateTime end = System.DateTime.Now;

            Debug.Log("Time taken: "+(end - start));
        }

        if (GUILayout.Button("Move Duplicate Materials Here"))
        {
            m_PathForDuplicates = GetSelectedPathOrFallback(true);
            MoveAllDuplicatesToFolder(m_PathForDuplicates, m_DuplicateMaterials);
        }

        if (GUILayout.Button("Move Duplicate Textures Here"))
        {
            m_PathForDuplicates = GetSelectedPathOrFallback(true);
            MoveAllDuplicatesToFolder(m_PathForDuplicates, m_DuplicateTextures);
        }

        if (GUILayout.Button("Move Duplicate Sprites Here"))
        {
            m_PathForDuplicates = GetSelectedPathOrFallback(true);
            MoveAllDuplicatesToFolder(m_PathForDuplicates, m_DuplicateSprites);
        }

    }

    public static void FindAllDuplicates()
    {
        InitializeLists();

        m_Materials = SetUniqueNamesInList(m_Materials);
        m_Textures = SetUniqueNamesInList(m_Textures);
        m_Sprites = SetUniqueNamesInList(m_Sprites);
        //m_AudioClips = SetUniqueNamesInList(m_AudioClips);
        //m_Meshes = SetUniqueNamesInList(m_Meshes);

        textureHashDictionary = CreateTextureHashDictionary(m_Textures);

        m_SpriteDuplicacyDictionary = CreateSpriteDuplicacyReferencesDictionary(m_Sprites);

        m_Textures = RemoveSpritesFromList(m_Sprites, m_Textures);

        m_TextureDuplicacyDictionary = CreateTextureDuplicacyReferencesDictionary(m_Textures);

        IncludeSpriteTexInTextureDictionary(m_SpriteDuplicacyDictionary);
        RereferenceDuplicateTexturesInAllMaterials(m_Materials, m_TextureDuplicacyDictionary);
        m_MaterialDuplicacyDictionary = CreateMaterialDuplicacyReferencesDictionary(m_Materials);

        //m_AudioClipDuplicacyDictionary = CreateAudioDuplicacyReferencesDictionary(m_AudioClips);
        //m_MeshDuplicacyDictionary = CreateMeshDuplicacyReferencesDictionary(m_Meshes, 0.001f);
    }

    public static void RunForScene()
    {
        m_SceneGameObjects = GetAllSceneGameObjects();
        ReReferenceAll(m_SceneGameObjects);
    }

    public static void RunApplication()
    {

        FindAllDuplicates();
        FindAllDuplicates();

        ReReferenceAll(m_PrefabObjects);
        ReReferenceAll(m_PrefabObjects);

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        string pathForcurrentScene = SceneManager.GetActiveScene().path; 

        for(int i = 0; i<scenes.Length; i++)
        {
            string path = scenes[i].path;
           
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            RunForScene();
            RunForScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        EditorSceneManager.OpenScene(pathForcurrentScene, OpenSceneMode.Single);

    }


    // We set unique names for every asset. If two have the same name, then one of them shall be suffixed with __0, __1
    // and so on. We need unique names so that we are able to store all duplicates in one folder.
    // If duplicates will have the same name, then we can't store them in the same folder and then
    // manual search is required which ultimately kills the whole point of this utility.
    public static List<T> SetUniqueNamesInList<T>(List<T> itemList) where T : Object
    {
        List<string> itemNamesList = new List<string>();
        List<T> updatedItems = new List<T>();

        foreach (T item in itemList)
        {

            if (item != null)
            {
                //string path = AssetDatabase.GetAssetPath(item);
                //TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                string name = item.name;
                int uniqueIndex = 0;
                while (itemNamesList.Contains(name))
                {
                    name = item.name + "__" + uniqueIndex.ToString();
                    uniqueIndex++;
                }

                item.name = name;
                //AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                itemNamesList.Add(name);

                updatedItems.Add(item);
            }
        }

        return updatedItems;

    }

    // Just a helper function in case you make 1000 materials and wan't to make each of it unique
    // TO BE DELETED
    static void AssignColors()
    {

        foreach (Material mat in m_Materials)
        {

            mat.color = new Color(r, g, b, 1);

            if (r <= 1f)
            {
                r = r + 0.01f;
            }
            else
            {
                r = 0;
                g = g + 0.01f;
            }

        }

    }

    // Self Explanatory
    static void InitializeLists()
    {
        m_Textures = GetAllOfType<Texture2D>("Texture2D");
        m_Materials = GetAllOfType<Material>("Material");
        m_Sprites = GetAllOfType<Sprite>("Sprite");
        m_AudioClips = GetAllOfType<AudioClip>("AudioClip");
        m_Meshes = GetAllOfType<Mesh>("Mesh");
        m_DuplicateMaterials = new List<Material>();
        m_DuplicateTextures = new List<Texture2D>();
        m_DuplicateSprites = new List<Sprite>();
        m_DuplicateAudioClips = new List<AudioClip>();
        m_DuplicateMeshes = new List<Mesh>();
        m_TextureDuplicacyDictionary = new Dictionary<Texture2D, Texture2D>();
        m_SpriteDuplicacyDictionary = new Dictionary<Sprite, Sprite>();
        m_MaterialDuplicacyDictionary = new Dictionary<Material, Material>();
        m_AudioClipDuplicacyDictionary = new Dictionary<AudioClip, AudioClip>();
        m_MeshDuplicacyDictionary = new Dictionary<Mesh, Mesh>();
        
        m_PrefabObjects = LoadAllPrefabGameObjects(Path.GetFullPath("Assets/"));
        m_SceneGameObjects = GetAllSceneGameObjects();
    }

    static void IncludeSpriteTexInTextureDictionary(Dictionary<Sprite, Sprite> spriteDictionary)
    {
        foreach (KeyValuePair<Sprite, Sprite> entry in spriteDictionary)
        {
            m_TextureDuplicacyDictionary.Add(entry.Key.texture, entry.Value.texture);
        }
    }

    // We remove the textures that are part of Sprites from the main texture list
    // since we are handling Sprites differently.
    static List<Texture2D> RemoveSpritesFromList(List<Sprite> sprites, List<Texture2D> textures)
    {

        foreach (Sprite sprite in sprites)
        {
            if (textures.Contains(sprite.texture))
            {
                textures.Remove(sprite.texture);
            }
        }

        return textures;

    }

    // Self Explanatory
    public static void MoveAllDuplicatesToFolder<T>(string path, List<T> duplicates) where T : Object
    {
        int appendedValue = 0;
        foreach (T dup in duplicates)
        {
            string currentPath = AssetDatabase.GetAssetPath(dup);
            string ext = Path.GetExtension(currentPath);
            string newPath = path + "/" + dup.name + ext;
            string n = AssetDatabase.MoveAsset(currentPath, newPath);
            while (n.Contains("Destination path name does already exist"))
            {
                appendedValue++;
                dup.name = dup.name + "__" + appendedValue.ToString();
                currentPath = AssetDatabase.GetAssetPath(dup);
                newPath = path + "/" + dup.name;
                n = AssetDatabase.MoveAsset(currentPath, newPath);
            }
        }
    }

    // Get a list of all GameObjects in the current active scene
    // AND ALSO THEIR CHILD OBJECTS
    static List<GameObject> GetAllSceneGameObjects()
    {
        List<GameObject> sceneObjects = new List<GameObject>();
        SceneManager.GetActiveScene().GetRootGameObjects(sceneObjects);

        sceneObjects = includeChildPrefabs(sceneObjects);

        return sceneObjects;
    }

    // Self Explanatory
    static Dictionary<Texture2D, string> CreateTextureHashDictionary(List<Texture2D> textureList)
    {

        Dictionary<Texture2D, string> textureHashDictionary = new Dictionary<Texture2D, string>();

        foreach (Texture2D tex in textureList)
        {
            if (tex)
            {
                string hash = "";
                SerializedProperty sp = new SerializedObject(tex).FindProperty("m_ImageContentsHash");
                while (sp.Next(true) && sp.propertyPath.StartsWith("m_ImageContentsHash"))
                    hash += sp.intValue.ToString();

                textureHashDictionary.Add(tex, hash);
            }
        }

        return textureHashDictionary;
    }

    // Go through all texture properties of all materials and re-reference
    // textures that are duplicate, consulting the duplicate texture look-up dictionary
    public static void RereferenceDuplicateTexturesInAllMaterials(List<Material> materials, Dictionary<Texture2D, Texture2D> texTotex)
    {

        foreach (Material mat in materials)
        {
            Shader s = mat.shader;
            int totalProperties = ShaderUtil.GetPropertyCount(s);

            for (int i = 0; i < totalProperties; i++)
            {
                if (ShaderUtil.GetPropertyType(s, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    string propertyName = ShaderUtil.GetPropertyName(s, i);

                    //Debug.Log("Material: "+mat + ", Property: "+propertyName);
                    Texture2D texOriginal;
                    try
                    {
                        texOriginal = (Texture2D)mat.GetTexture(propertyName);
                        if (texOriginal && texTotex.ContainsKey(texOriginal))
                        {
                            Texture2D texNew = texTotex[texOriginal];
                            mat.SetTexture(propertyName, texNew);
                        }
                    }
                    catch (Ex e)
                    {
                        Debug.Log("CAUGHT: " + e);
                    }
                }
            }
        }

    }

    // Get a List of all Assets that are of type T.
    // "ofType" is just the string version of T. eg. T = Material, ofType = "Material"
    static List<T> GetAllOfType<T>(string ofType) where T : Object
    {

        List<T> typeToPathMapping = new List<T>();
        string[] lookIn = { "Assets" };
        string[] guids = AssetDatabase.FindAssets("t:" + ofType, lookIn);

        string assetPath;

        foreach (string guid in guids)
        {
            assetPath = AssetDatabase.GUIDToAssetPath(guid);
            T generic = AssetDatabase.LoadAssetAtPath<T>(assetPath);

            if (generic != null)
                typeToPathMapping.Add(generic);
        }

        return typeToPathMapping;
    }

    // Look-up Dictionary for duplicate textures
    public static Dictionary<Texture2D, Texture2D> CreateTextureDuplicacyReferencesDictionary(List<Texture2D> textureList)
    {
        Dictionary<Texture2D, Texture2D> textureDuplicacyDictionary = new Dictionary<Texture2D, Texture2D>();
        int totalTextures = textureList.Count;
        List<Texture2D> m_BufferTextures = new List<Texture2D>(textureList);


        for (int i = 0; i < totalTextures; i++)
        {

            textureDuplicacyDictionary.Add(m_BufferTextures[i], m_BufferTextures[i]);

            for (int j = i + 1; j < totalTextures; j++)
            {
                bool areEqual = CheckTextureEquality(m_BufferTextures[i], m_BufferTextures[j], textureHashDictionary, true);

                if (areEqual)
                {
                    textureDuplicacyDictionary.Add(m_BufferTextures[j], m_BufferTextures[i]);
                    m_DuplicateTextures.Add(m_BufferTextures[j]);
                    m_BufferTextures.RemoveAt(j);
                    j--;
                    totalTextures = m_BufferTextures.Count;

                }
            }

        }

        return textureDuplicacyDictionary;

    }

    // Look-up Dictionary for duplicate sprites
    public static Dictionary<Sprite, Sprite> CreateSpriteDuplicacyReferencesDictionary(List<Sprite> spriteList)
    {
        int totalSprites = spriteList.Count;
        List<Sprite> m_BufferSprites = new List<Sprite>(spriteList);
        Dictionary<Sprite, Sprite> spriteDictionary = new Dictionary<Sprite, Sprite>();

        for (int i = 0; i < totalSprites; i++)
        {

            Texture2D texi = m_BufferSprites[i].texture;
            spriteDictionary.Add(m_BufferSprites[i], m_BufferSprites[i]);
            m_TextureDuplicacyDictionary.Add(texi, texi);

            for (int j = i + 1; j < totalSprites; j++)
            {

                Texture2D texj = m_BufferSprites[j].texture;
                bool areEqual = false;

                areEqual = CheckTextureEquality(texi, texj, textureHashDictionary, true);

                if (areEqual)
                {
                    spriteDictionary.Add(m_BufferSprites[j], m_BufferSprites[i]);
                    m_TextureDuplicacyDictionary.Add(texj, texi);
                    m_DuplicateSprites.Add(m_BufferSprites[j]);
                    m_BufferSprites.RemoveAt(j);
                    j--;
                    totalSprites = m_BufferSprites.Count;
                }

            }

        }


        return spriteDictionary;

    }

    // Look-up dictionary
    public static Dictionary<Material, Material> CreateMaterialDuplicacyReferencesDictionary(List<Material> materialList)
    {
        Dictionary<Material, Material> materialDuplicacyDictionary = new Dictionary<Material, Material>();
        int totalMaterials = materialList.Count;
        List<Material> m_BufferMaterials = new List<Material>(materialList);


        for (int i = 0; i < totalMaterials; i++)
        {

            materialDuplicacyDictionary.Add(m_BufferMaterials[i], m_BufferMaterials[i]);

            for (int j = i + 1; j < totalMaterials; j++)
            {
                bool areEqual = CheckMaterialEquality(m_BufferMaterials[i], m_BufferMaterials[j]);

                if (areEqual)
                {
                    materialDuplicacyDictionary.Add(m_BufferMaterials[j], m_BufferMaterials[i]);
                    m_DuplicateMaterials.Add(m_BufferMaterials[j]);
                    m_BufferMaterials.RemoveAt(j);
                    j--;
                    totalMaterials = m_BufferMaterials.Count;

                }
            }

        }

        return materialDuplicacyDictionary;

    }

    // Look-up Dictionary for duplicate sprites
    public static Dictionary<AudioClip, AudioClip> CreateAudioDuplicacyReferencesDictionary(List<AudioClip> audioClipList)
    {
        int totalAudioClips = audioClipList.Count;
        List<AudioClip> m_BufferAudio = new List<AudioClip>(audioClipList);
        Dictionary<AudioClip, AudioClip> audioDictionary = new Dictionary<AudioClip, AudioClip>();

        for (int i = 0; i < totalAudioClips; i++)
        {

            audioDictionary.Add(m_BufferAudio[i], m_BufferAudio[i]);

            for (int j = i + 1; j < totalAudioClips; j++)
            {

                bool areEqual = false;

                areEqual = CompareAudioFiles(m_BufferAudio[j], m_BufferAudio[j]);

                if (areEqual)
                {
                    audioDictionary.Add(m_BufferAudio[j], m_BufferAudio[i]);
                    m_DuplicateAudioClips.Add(m_BufferAudio[j]);
                    m_BufferAudio.RemoveAt(j);
                    j--;
                    totalAudioClips = m_BufferAudio.Count;
                }

            }

        }


        return audioDictionary;

    }


    public static Dictionary<Mesh, Mesh> CreateMeshDuplicacyReferencesDictionary(List<Mesh> meshList, float tolerance)
    {
        Dictionary<Mesh, Mesh> duplicacyDictionary = new Dictionary<Mesh, Mesh>();
        List<Mesh> bufferMeshList = new List<Mesh>(meshList);

        int totalMeshes = bufferMeshList.Count;

        for (int i = 0; i < totalMeshes; i++)
        {

            duplicacyDictionary.Add(bufferMeshList[i], bufferMeshList[i]);

            for (int j = i + 1; j < totalMeshes; j++)
            {
                if (CompareMeshes(bufferMeshList[i], bufferMeshList[j], 0.01f))
                {
                    duplicacyDictionary.Add(bufferMeshList[j], bufferMeshList[i]);
                    m_DuplicateMeshes.Add(bufferMeshList[j]);
                    bufferMeshList.RemoveAt(j);
                    j--;
                    totalMeshes = bufferMeshList.Count;
                }
            }

        }

        return duplicacyDictionary;
    }


    // Get a list of all prefabs on the given full-path
    public static List<GameObject> LoadAllPrefabGameObjects(string fullpath)
    {

        // Searching all prefabs, even in the Sub-directories
        string[] filenames = Directory.GetFiles(fullpath, "*.prefab", SearchOption.AllDirectories);

        List<GameObject> prefabs = new List<GameObject>();

        //loop through directory loading the game object and checking if it has the component you want
        foreach (string filename in filenames)
        {
            string fullPath = filename.Replace(@"\", "/");
            string assetPath = "Assets" + fullPath.Replace(Application.dataPath, "");
            GameObject prefab = AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)) as GameObject;
            // If prefab is not null, we get its children, if any, as well and add them to the list
            if (prefab != null)
            {
                prefabs = includeChildPrefabs(prefabs, prefab);
            }
        }

        return prefabs;
    }


    // Include all the children of the Gameobjects given in the list that was passed and return
    // the updated list.
    public static List<GameObject> includeChildPrefabs(List<GameObject> listOfParentPrefabs)
    {
        List<GameObject> allGameobjects = new List<GameObject>();

        foreach (GameObject obj in listOfParentPrefabs)
        {
            allGameobjects.Add(obj);
            GetAllChildren(obj, allGameobjects);

        }

        return allGameobjects;
    }

    public static void GetAllChildren(GameObject obj, List<GameObject> childrenList)
    {
        Transform transform = obj.GetComponent<Transform>();
        int count = transform.childCount;

        GameObject[] children = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            Transform transChild = transform.GetChild(i);
            children[i] = transChild.gameObject;
            childrenList.Add(children[i]);

            if (transChild.childCount > 0)
            {
                GetAllChildren(transChild.gameObject, childrenList);
            }
        }

        //return children;
    }


    // Include all the children of the given GameObject only and update the list.
    public static List<GameObject> includeChildPrefabs(List<GameObject> listOfParentPrefabs, GameObject obj)
    {

        GetAllChildren(obj, listOfParentPrefabs);

        /*
        // Get all the children of Parent Gameobject and store them in an array
        Transform[] childObjects = obj.GetComponentsInChildren<Transform>();

        if (childObjects.Length > 0)
        {

            // loop through all the Child Gameobjects
            foreach (Transform gObject in childObjects)
            {

                // child gameobject should not be null and should not already be present in the List
                if (gObject != null && !listOfParentPrefabs.Contains(gObject.gameObject))
                {
                    listOfParentPrefabs.Add(gObject.gameObject);
                }
            }

        }
        */
        return listOfParentPrefabs;
    }


    static bool CheckMaterialEquality(Material m1, Material m2)
    {
        bool isSame = true;

        int totalProperties1, totalProperties2;

        string currentProperty = "";

        if (m1 && m2)
        {

            Shader s1 = m1.shader, s2 = m2.shader;

            // From here, we start checking for each and every property of material
            // Property names like "_Mode", "_Metallic" etc. have been taken from Standard Shader Script
            if (s1 == s2)
            {

                totalProperties1 = ShaderUtil.GetPropertyCount(s1);
                totalProperties2 = ShaderUtil.GetPropertyCount(s2);

                if (totalProperties1 != totalProperties2)
                {

                    isSame = false;

                }
                else
                {

                    for (int i = 0; i < totalProperties1; i++)
                    {

                        currentProperty = ShaderUtil.GetPropertyName(s1, i);

                        if (!m2.HasProperty(currentProperty))
                        {

                            isSame = false;
                            break;

                        }
                        else
                        {

                            if (!ArePropertiesSame(m1, m2, i, currentProperty, false))
                            {

                                isSame = false;
                                break;
                            }

                        }

                    }

                }

            }
            else
            {

                isSame = false;

            }

        }
        else
        {

            isSame = false;

        }

        return isSame;
    }

    static bool CompareMeshes(Mesh mesh1, Mesh mesh2, float tolerance)
    {

        bool areSimilar = true;

        Vector3 pivot1 = new Vector3(mesh1.bounds.center.x, mesh1.bounds.center.y, mesh1.bounds.center.z);
        Vector3 pivot2 = new Vector3(mesh2.bounds.center.x, mesh2.bounds.center.y, mesh2.bounds.center.z);

        if (pivot1 == pivot2)
        {
            int totalVertices1 = mesh1.vertexCount;
            int totalVertices2 = mesh2.vertexCount;

            if (totalVertices1 == totalVertices2)
            {
                Vector3[] verts1 = mesh1.vertices;
                Vector3[] verts2 = mesh2.vertices;

                for (int i = 0; i < totalVertices1; i++)
                {

                    if (verts1[i].x - verts2[i].x >= tolerance)
                    {
                        return false;
                    }
                    else
                    {
                        if (verts1[i].y - verts2[i].y >= tolerance)
                        {
                            return false;
                        }
                        else
                        {
                            if (verts1[i].z - verts2[i].z >= tolerance)
                            {
                                return false;
                            }
                        }
                    }

                }


                Vector3[] norms1 = mesh1.normals;
                Vector3[] norms2 = mesh2.normals;

                int totalnorms1 = norms1.Length;
                int totalnorms2 = norms2.Length;

                if (totalnorms1 == totalnorms2)
                {

                    for (int i = 0; i < totalnorms1; i++)
                    {
                        if (norms1[i].x - norms2[i].x >= tolerance)
                        {
                            return false;
                        }
                        else
                        {
                            if (norms1[i].y - norms2[i].y >= tolerance)
                            {
                                return false;
                            }
                            else
                            {
                                if (norms1[i].z - norms2[i].z >= tolerance)
                                {
                                    return false;
                                }
                            }
                        }
                    }

                    Color[] colors1 = mesh1.colors;
                    Color[] colors2 = mesh2.colors;

                    int totalColors1 = colors1.Length;
                    int totalColors2 = colors2.Length;

                    if (totalColors1 == totalColors2)
                    {

                        for (int i = 0; i < totalColors1; i++)
                        {
                            if (colors1[i].r - colors2[i].r >= tolerance)
                            {
                                return false;
                            }
                            else
                            {
                                if (colors1[i].g - colors2[i].g >= tolerance)
                                {
                                    return false;
                                }
                                else
                                {
                                    if (colors1[i].b - colors2[i].b >= tolerance)
                                    {
                                        return false;
                                    }
                                    else
                                    {
                                        if (colors1[i].a - colors2[i].a >= tolerance)
                                        {
                                            return false;
                                        }
                                    }
                                }
                            }
                        }


                        Vector2[] uv1mesh1 = mesh1.uv;
                        Vector2[] uv1mesh2 = mesh2.uv;

                        int totalUv1mesh1 = uv1mesh1.Length;
                        int totalUv1mesh2 = uv1mesh2.Length;

                        if (totalUv1mesh1 == totalUv1mesh2)
                        {
                            for (int i = 0; i < totalUv1mesh1; i++)
                            {
                                if (uv1mesh1[i].x - uv1mesh2[i].x >= tolerance)
                                {
                                    return false;
                                }
                                else
                                {
                                    if (uv1mesh1[i].y - uv1mesh2[i].y >= tolerance)
                                    {
                                        return false;
                                    }
                                }
                            }
                        }

                    }
                }


            }
            else
            {
                return false;
            }

        }
        else
        {
            return false;
        }

        return areSimilar;

    }

    public static bool CheckTextureSimilarityInList(Texture2D t1, Texture2D t2, List<Texture2D> textureList)
    {

        int index1 = textureList.IndexOf(t1);
        int index2 = textureList.IndexOf(t2);

        if (index1 == index2)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    // checking if both the materials have the same value of a particular property
    static bool ArePropertiesSame(Material m1, Material m2, int propertyIndex, string propertyName, bool texCheckPerPixel)
    {

        Shader s1 = m1.shader, s2 = m2.shader;

        bool areSame = true;

        if (ShaderUtil.GetPropertyType(s1, propertyIndex) == ShaderUtil.GetPropertyType(s2, propertyIndex))
        {

            switch (ShaderUtil.GetPropertyType(s1, propertyIndex))
            {

                case ShaderUtil.ShaderPropertyType.Color:
                    if (m1.GetColor(propertyName) != m2.GetColor(propertyName))
                    {

                        areSame = false;

                    }
                    break;

                case ShaderUtil.ShaderPropertyType.Float:
                    if (m1.GetFloat(propertyName) != m2.GetFloat(propertyName))
                    {

                        areSame = false;

                    }
                    break;

                case ShaderUtil.ShaderPropertyType.TexEnv:

                    try
                    {
                        if (!CheckTextureSimilarityInList((Texture2D)m1.GetTexture(propertyName),
                                            (Texture2D)m2.GetTexture(propertyName), m_Textures))
                        {

                            areSame = false;

                        }

                    }
                    catch (Ex e)
                    {
                        Debug.Log("CAUGHT: " + e);
                        areSame = false;
                    }

                    break;

                case ShaderUtil.ShaderPropertyType.Vector:
                    if (m1.GetVector(propertyName) != m2.GetVector(propertyName))
                    {

                        areSame = false;

                    }
                    break;

                case ShaderUtil.ShaderPropertyType.Range:
                    if (m1.GetFloat(propertyName) != m2.GetFloat(propertyName))
                    {

                        areSame = false;

                    }
                    break;
            }

        }

        return areSame;

    }


    static bool CheckTextureEquality(Texture2D t1, Texture2D t2, Dictionary<Texture2D, string> textureHashDictionary, bool checkPerPixel)
    {
        if (t1 && t2)
        {
            if (textureHashDictionary.ContainsKey(t1) && textureHashDictionary.ContainsKey(t2) &&
                (textureHashDictionary[t1].Equals(textureHashDictionary[t2])))
            {

                if (t1.alphaIsTransparency == t1.alphaIsTransparency)
                {

                    if (t1.anisoLevel == t2.anisoLevel)
                    {

                        if (t1.dimension == t2.dimension)
                        {

                            if (t1.filterMode == t2.filterMode)
                            {

                                if (t1.format == t2.format)
                                {

                                    if (t1.height == t2.height)
                                    {

                                        if (t1.hideFlags == t2.hideFlags)
                                        {

                                            if (t1.mipMapBias == t2.mipMapBias)
                                            {

                                                if (t1.mipmapCount == t2.mipmapCount)
                                                {

                                                    if (t1.texelSize == t2.texelSize)
                                                    {

                                                        if (t1.width == t2.width)
                                                        {

                                                            if (t1.wrapMode == t2.wrapMode)
                                                            {

                                                                if (checkPerPixel)
                                                                {
                                                                    bool areEqual = CompareTexturePixels(t1, t2);
                                                                    return areEqual;
                                                                }
                                                                else
                                                                    return true;

                                                            }

                                                        }

                                                    }

                                                }

                                            }

                                        }

                                    }

                                }

                            }

                        }

                    }

                }

            }

        }
        else if (t1 == null && t2 == null)
        {
            return true;
        }

        return false;
    }


    static bool CompareTexturePixels(Texture2D t1, Texture2D t2)
    {

        if (t1 && t2)
        {


            TextureImporter importer1, importer2;
            string path1, path2;

            path1 = AssetDatabase.GetAssetPath(t1);
            importer1 = AssetImporter.GetAtPath(path1) as TextureImporter;

            path2 = AssetDatabase.GetAssetPath(t2);
            importer2 = AssetImporter.GetAtPath(path2) as TextureImporter;


            if (importer1 && importer2)
            {
                if (!importer1.isReadable)
                {
                    importer1.isReadable = true;
                    AssetDatabase.ImportAsset(path1, ImportAssetOptions.ForceUpdate);
                }

                if (!importer2.isReadable)
                {
                    importer2.isReadable = true;
                    AssetDatabase.ImportAsset(path2, ImportAssetOptions.ForceUpdate);
                }



                Color[] t1Colors = t1.GetPixels();
                Color[] t2Colors = t2.GetPixels();
                bool areEqual = true;

                int totalPixelsT1 = t1Colors.Length;
                int totalPixelsT2 = t2Colors.Length;

                if (totalPixelsT1 == totalPixelsT2)
                {
                    for (int i = 0; i < totalPixelsT1; i++)
                    {
                        if (t1Colors[i] != t2Colors[i])
                        {
                            areEqual = false;
                            break;
                        }
                    }
                }


                return areEqual;

            }
            else
            {
                return false;
            }
        }
        else if (!t1 && !t2)
        {
            return true;
        }
        else
        {
            return false;
        }
    }


    static bool CompareAudioFiles(AudioClip audioClip1, AudioClip audioClip2)
    {

        

        return true;
    }


    // We determine the path of the Folder selected in the Project Window
    public static string GetSelectedPathOrFallback(bool onlyProjectPath)
    {
        string path = "Assets";

        foreach (UnityEngine.Object obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
        {
            path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                path = Path.GetDirectoryName(path);
                break;
            }
        }


        if (path != "")
        {
            if (path.EndsWith("/"))
            {
                path = path.TrimEnd('/');
            }
        }

        if (!onlyProjectPath)
        {
            string completePath = Application.dataPath.Replace("Assets", "") + path;
            return completePath;
        }
        else
        {
            return path;
        }

    }


    static void ReReferenceAll(List<GameObject> objs)
    {

        if (objs != null)
        {

            foreach (GameObject objct in objs)
            {

                //Debug.Log(objct.name);
                if (objct != null)
                {

                    Component[] cs = objct.GetComponents(typeof(Component));

                    if (cs != null)
                    {
                        foreach (Component c in cs)
                        {
                            //Debug.Log("Comp name " + c.name + " type " + c.GetType() + " basetype " + c.GetType().BaseType);

                            if (c != null)
                            {

                                System.Type type = c.GetType();

                                while (type != typeof(Object))
                                {
                                    
                                    foreach (FieldInfo fi in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic |
                                                                BindingFlags.Public | BindingFlags.Static))
                                    {

                                        if (fi != null)
                                        {
                                            System.Object obj = (System.Object)c;
                                            //Debug.Log("fi name " + fi.Name + " val " + fi.GetValue(obj));

                                            if (obj != null)
                                            {

                                                if (fi.GetValue(obj) != null)
                                                {

                                                    if (fi.GetValue(obj).GetType() == typeof(Sprite))
                                                    {
                                                        Sprite sp = (Sprite)fi.GetValue(obj);

                                                        if (sp)
                                                        {
                                                            if (m_SpriteDuplicacyDictionary.ContainsKey(sp))
                                                            {
                                                                fi.SetValue(obj, m_SpriteDuplicacyDictionary[sp]);
                                                            }
                                                        }
                                                    }

                                                    if ((fi.GetValue(obj).GetType() == typeof(Texture2D)))
                                                    {
                                                        Texture2D tex = (Texture2D)fi.GetValue(obj);

                                                        if (tex)
                                                        {
                                                            if (m_TextureDuplicacyDictionary.ContainsKey(tex))
                                                            {
                                                                fi.SetValue(obj, m_TextureDuplicacyDictionary[tex]);
                                                                //Debug.Log("Tex2D: SET IT UPPPPPPPPPPPPPPPPPPPP");
                                                            }
                                                        }
                                                    }

                                                    if (fi.GetValue(obj).GetType() == typeof(Material))
                                                    {
                                                        Material mat = (Material)fi.GetValue(obj);

                                                        if (mat != null)
                                                        {

                                                            if (m_MaterialDuplicacyDictionary.ContainsKey(mat))
                                                            {
                                                                fi.SetValue(obj, m_MaterialDuplicacyDictionary[mat]);
                                                                //Debug.Log("Material: SET IT UPPPPPPPPPPPPPPPPPPPP");
                                                            }

                                                        }
                                                    }

                                                    if (fi.GetValue(obj).GetType() == typeof(Mesh))
                                                    {
                                                        Mesh mesh = (Mesh)fi.GetValue(obj);

                                                        if (mesh != null)
                                                        {

                                                            if (m_MeshDuplicacyDictionary.ContainsKey(mesh))
                                                            {
                                                                fi.SetValue(obj, m_MeshDuplicacyDictionary[mesh]);
                                                                //Debug.Log("Material: SET IT UPPPPPPPPPPPPPPPPPPPP");
                                                            }

                                                        }
                                                    }

                                                    if (fi.GetValue(obj).GetType() == typeof(Sprite[]))
                                                    {
                                                        Sprite[] sprites = (Sprite[])fi.GetValue(obj);

                                                        for (int i = 0; i < sprites.Length; i++)
                                                        {
                                                            if (sprites[i])
                                                            {
                                                                if (m_SpriteDuplicacyDictionary.ContainsKey(sprites[i]))
                                                                {
                                                                    sprites[i] = m_SpriteDuplicacyDictionary[sprites[i]];
                                                                    //Debug.Log("Array: SET IT UPPPPPPPPPPPPPPPPPPPP");
                                                                }
                                                            }

                                                        }

                                                        fi.SetValue(obj, sprites);

                                                    }

                                                    if (fi.GetValue(obj).GetType() == typeof(Material[]))
                                                    {
                                                        Material[] mats = (Material[])fi.GetValue(obj);

                                                        for (int i = 0; i < mats.Length; i++)
                                                        {
                                                            if (mats[i])
                                                            {
                                                                if (m_MaterialDuplicacyDictionary.ContainsKey(mats[i]))
                                                                {
                                                                    mats[i] = m_MaterialDuplicacyDictionary[mats[i]];
                                                                    //Debug.Log("Array: SET IT UPPPPPPPPPPPPPPPPPPPP");
                                                                }
                                                            }

                                                        }

                                                        fi.SetValue(obj, mats);

                                                    }

                                                    if (fi.GetValue(obj).GetType() == typeof(Texture2D[]))
                                                    {
                                                        Texture2D[] texes = (Texture2D[])fi.GetValue(obj);

                                                        for (int i = 0; i < texes.Length; i++)
                                                        {

                                                            if (texes[i])
                                                            {
                                                                if (m_TextureDuplicacyDictionary.ContainsKey(texes[i]))
                                                                {
                                                                    texes[i] = m_TextureDuplicacyDictionary[texes[i]];
                                                                    //Debug.Log("Array: SET IT UPPPPPPPPPPPPPPPPPPPP");
                                                                }
                                                            }

                                                        }

                                                        fi.SetValue(obj, texes);

                                                    }

                                                    if (fi.GetValue(obj).GetType() == typeof(Mesh[]))
                                                    {
                                                        Mesh[] meshes = (Mesh[])fi.GetValue(obj);

                                                        for (int i = 0; i < meshes.Length; i++)
                                                        {

                                                            if (meshes[i])
                                                            {
                                                                if (m_MeshDuplicacyDictionary.ContainsKey(meshes[i]))
                                                                {
                                                                    meshes[i] = m_MeshDuplicacyDictionary[meshes[i]];
                                                                    //Debug.Log("Array: SET IT UPPPPPPPPPPPPPPPPPPPP");
                                                                }
                                                            }

                                                        }

                                                        fi.SetValue(obj, meshes);

                                                    }

                                                }
                                            }

                                        }
                                    }


                                    foreach (PropertyInfo pi in type.GetProperties())
                                    {
                                        try
                                        {
                                            object obj = c;

                                            if (pi.Name.Equals("sharedMaterials"))
                                            {
                                                if (pi.GetValue(obj, null) != null)
                                                {
                                                    if (pi.GetValue(obj, null).GetType() == typeof(Material[]))
                                                    {
                                                        Material[] mats = (Material[])pi.GetValue(obj, null);

                                                        for (int i = 0; i < mats.Length; i++)
                                                        {

                                                            if (mats[i])
                                                            {
                                                                if (m_MaterialDuplicacyDictionary.ContainsKey(mats[i]))
                                                                {
                                                                    mats[i] = m_MaterialDuplicacyDictionary[mats[i]];
                                                                    //Debug.Log("Found");
                                                                }
                                                            }
                                                        }

                                                        pi.SetValue(obj, mats, null);
                                                        //Debug.Log("Check");
                                                    }


                                                }
                                            }
                                            
                                            if (pi.Name.Equals("sharedMesh"))
                                            {
                                                if (pi.GetValue(obj, null) != null)
                                                {
                                                    if (pi.GetValue(obj, null).GetType() == typeof(Mesh[]))
                                                    {
                                                        Mesh[] meshes = (Mesh[])pi.GetValue(obj, null);

                                                        for (int i = 0; i < meshes.Length; i++)
                                                        {

                                                            if (meshes[i])
                                                            {
                                                                if (m_MeshDuplicacyDictionary.ContainsKey(meshes[i]))
                                                                {
                                                                    meshes[i] = m_MeshDuplicacyDictionary[meshes[i]];
                                                                    //Debug.Log("Found");
                                                                }
                                                            }
                                                        }

                                                        pi.SetValue(obj, meshes, null);
                                                        //Debug.Log("Check");
                                                    }


                                                }
                                            }

                                        }
                                        catch (System.Reflection.TargetInvocationException ne)
                                        {
                                            Debug.Log("CAUGHT: " + ne.GetType());
                                        }
                                        catch (NotSupported ne)
                                        {
                                            Debug.Log("CAUGHT: " + ne.GetType());
                                        }

                                    }

                                    type = type.BaseType;
                                }

                            }
                        }

                    }
                }
            }
        }
    }


}
