using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using NaughtyAttributes;

public sealed class Experimenter : MonoBehaviour
{
    // ----- Constants -----
    private const string ExperimentParams = "Experiment Parameters";

    private const string MainFolderName = "Landscaper Ants";
    private const string PrintsFolderName = "Screenshots";

    private const string FolderNamePrefix = "Experiment";
    private const string FileNamePrefix = "Params";
    private const string PrintsPrefix = "Screenshot";

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

    private void CreateDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public int SecureHash(int input)
    {
        using SHA256 sha256 = SHA256.Create();

        byte[] inputBytes = Encoding.UTF8.GetBytes(input.ToString());
        byte[] hashBytes = sha256.ComputeHash(inputBytes);

        // Convert the hash bytes to an integer
        int hash = BitConverter.ToInt32(hashBytes, 0);

        return hash;
    }

    public void PrintScreen(int index, int imgIndex, int step)
    {
        Texture2D print = ScreenCapture.CaptureScreenshotAsTexture();

        byte[] bytes = print.EncodeToPNG();

        Destroy(print);

        string filePath = Path.Combine(docsPath, MainFolderName, 
            $"{FolderNamePrefix}_{index}", PrintsFolderName, $"{PrintsPrefix}_{imgIndex}-Step_{step}.png");

        File.WriteAllBytes(filePath, bytes);
    }

    public void CreateMainDirectory()
    {
        string mainPath = Path.Combine(docsPath, MainFolderName);
        CreateDirectory(mainPath);
    }

    public void CreateExperimentDirectory(int index)
    {
        // Create an experiment's folder
        string experimentPath = Path.Combine(docsPath, MainFolderName, $"{FolderNamePrefix}_{index}");
        CreateDirectory(experimentPath);

        // Create the experiment's screenshots' folder, inside the previously created
        string screenshotsPath = Path.Combine(experimentPath, PrintsFolderName);
        CreateDirectory(screenshotsPath);
    }

    public void CreateParamsFile(int index, string content)
    {
        string filePath = Path.Combine(docsPath, MainFolderName, $"{FolderNamePrefix}_{index}", $"{FileNamePrefix}_{index}.txt");

        // Create a txt file and write the experiment's parameters in it
        using (StreamWriter sw = File.CreateText(filePath))
        {
            sw.Write(content);
        }

        // Set the file as read only after writing
        File.SetAttributes(filePath, FileAttributes.ReadOnly);
    }

    public bool ParamsFileExits(int index)
    {
        // Checks if the txt file containing the experiment's parameters exists
        string filePath = Path.Combine(docsPath, MainFolderName, $"{FolderNamePrefix}_{index}", $"{FileNamePrefix}_{index}.txt");
        return File.Exists(filePath);
    }
}
