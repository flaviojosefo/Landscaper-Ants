using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using NaughtyAttributes;

public sealed class Experimenter : MonoBehaviour
{
    // ----- Constants -----
    private const string ExperimentParams = "Experiment Parameters";

    private const string FileNamePrefix = "Experiment";
    private const string MainFolderName = "Landscaper Ants";
    private const string PrintsFolderName = "Screenshots";

    // ----- Private EDITOR config parameters -----

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private int[] seeds;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private bool[] shuffleAnts;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private bool[] individualStart;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private int[] nAnts;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private int maxSteps;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private int printStep;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private bool[] antsInPlace;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private bool[] absSlope;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private Vector4[] weights;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private float[] phEvap;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private float[] phDiff;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private Vector2[] heightIncr;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private bool[] flatTerrain;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private int[] foodAmount;

    [BoxGroup(ExperimentParams)]
    [SerializeField]
    private int maxFoodBites;

    // ----- Private instance variables -----

    private readonly string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    // ----- Public parameter properties -----

    public int[] Seeds => seeds;

    public bool[] ShuffleAnts => shuffleAnts;

    public bool[] IndividualStart => individualStart;

    public int[] NAnts => nAnts;

    public int MaxSteps => maxSteps;

    public int PrintStep => printStep;

    public bool[] AntsInPlace => antsInPlace;

    public bool[] AbsSlope => absSlope;

    public Vector4[] Weights => weights;

    public float[] PhEvap => phEvap;

    public float[] PhDiff => phDiff;

    public Vector2[] HeightIncr => heightIncr;

    public bool[] FlatTerrain => flatTerrain;

    public int[] FoodAmount => foodAmount;

    public int MaxFoodBites => maxFoodBites;

    // ----- METHODS -----

    public int SecureHash(int input)
    {
        using SHA256 sha256 = SHA256.Create();

        byte[] inputBytes = Encoding.UTF8.GetBytes(input.ToString());
        byte[] hashBytes = sha256.ComputeHash(inputBytes);

        // Convert the hash bytes to an integer
        int hash = BitConverter.ToInt32(hashBytes, 0);

        return hash;
    }

    private void CreateDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private void CreateFile(string path, string fileName)
    {
        string filePath = Path.Combine(path, $"{fileName}.txt");

        if (!File.Exists(filePath))
        {
            using (StreamWriter sw = File.CreateText(filePath))
            {
                sw.Write("Hello!");
            }

            File.SetAttributes(filePath, FileAttributes.ReadOnly);
        }
    }

    [Button]
    private void TakeScreenshot()
    {
        SceneView sceneWindow = EditorWindow.GetWindow<SceneView>();

        Rect winRect = sceneWindow.position;

        Color[] colors = InternalEditorUtility.ReadScreenPixel(winRect.position, (int)winRect.width, (int)winRect.height);

        Texture2D result = new((int)winRect.width, (int)winRect.height);
        result.SetPixels(colors);

        byte[] bytes = result.EncodeToPNG();

        DestroyImmediate(result);

        DateTime dt = DateTime.Now;
        string timeString = string.Format($"{dt.Year}-{dt.Month:00}-{dt.Day:00}_{dt.Hour:00}-{dt.Minute:00}-{dt.Second:00}");

        File.WriteAllBytes(Path.Combine(Application.dataPath, "Screenshots", $"Screenshot_{timeString}.png"), bytes);

        AssetDatabase.Refresh();
    }

    [Button]
    private void CreateExperiments()
    {
        string mainPath = Path.Combine(docsPath, MainFolderName);

        CreateDirectory(MainFolderName);

        for (int i = 0; i < 10; i++)
        {
            string experimentPath = Path.Combine(mainPath, $"{FileNamePrefix}_{i}");

            CreateDirectory(experimentPath);

            string screenshotsPath = Path.Combine(experimentPath, PrintsFolderName);

            CreateDirectory(screenshotsPath);

            // Create file AFTER FULL EXPERIMENT
            // Check if this file exists in the BEGINNING OF THE EXPERIMENT
            // to check if a full run was already done
            // How to handle already created screenshots IF the experimemt was stopped???
            CreateFile(experimentPath, $"Params_{i}");
        }
    }
}
