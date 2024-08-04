using UnityEngine;
using UnityEditor;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

/// <summary>
/// This Editor script checks the project's location for optimal Unity operation.
/// The project must not be on Desktop, in the Documents folder or in a folder synchronized by OneDrive or other synchronization service.
/// The project path must not contain accents and must not exceed 90 characters.
/// If the project is in an invalid location, an error message is displayed in the console and a red gizmo is displayed in the scene.
///  
/// It executes the verification once when the Unity editor is loaded.
/// It is compatible with Windows and MacOS.
/// It must be placed in an Editor folder (inside the Assets or Packages folder).
/// 
/// Created by Jonathan Tremblay, teacher at Cegep de Saint-Jerome.
/// This project is available for distribution and modification under the CC0 License.
/// https://github.com/JonathanTremblay/UnityProjectPathChecker
/// </summary>
namespace ProjectPathChecker
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public class ProjectPathChecker
    {
        static readonly string currentVersion = "Version 0.9.1 (2024-08-05)";

        private static readonly bool _showPositiveMessages = true; // If true, the positive message will be shown. If false, only the negative messages will be shown.

        private static readonly Dictionary<string, string> _messagesEn = new() // A dictionary for English messages
        {
            {"ABOUT", $"<size=10>** Project Path Checker is free and open source. For updates and feedback, visit https://github.com/JonathanTremblay/UnityProjectPathChecker. **</size>\n<size=10>** {currentVersion} **</size>"},
            {"INVALID_TITLE", "INVALID PROJECT LOCATION"},
            {"MORE_INFO", "(For more information, see the message in the console.)"},
            {"EXPLANATION_START", "The project is"},
            {"WHY", "This is a problem because this folder can be synchronized by OneDrive."},
            {"SOLUTION_MOVE", "Solution: 1. Quit Unity ; 2. Move the project to another folder ; 3. Reopen the project."},
            {"SOLUTION_RENAME", "Solution: 1. Quit Unity ; 2. Remove accents from the structure where the project is located ; 3. Reopen the project."},
            {"PATH_TOO_LONG", "in a folder whose path is too long ({0} characters). The path should not exceed {1} characters."},
            {"ACCENTED_CHARACTERS", "in a folder whose path contains accented characters."},
            {"CLOUD_FOLDER", "in a {0} folder."},
            {"DOCUMENTS_FOLDER", "in the OS's Documents folder."},
            {"ON_DESKTOP", "on the desktop."},
            {"CAUSE_PATH_TOO_LONG", "path too long"},
            {"CAUSE_ACCENTED_CHARACTERS", "accented characters"},
            {"CONGRATS", "Congratulations, the project is in a suitable folder: short path, no accents, not in a OneDrive folder."},
            {"CURRENT_LOCATION", "Current location:"}
        };

        private static readonly Dictionary<string, string> _messagesFr = new() // A dictionary for French messages
        {
            {"ABOUT", $"<size=10>** Project Path Checker est gratuit et open source. Pour les mises à jour et les commentaires, visitez https://github.com/JonathanTremblay/UnityProjectPathChecker. **</size>\n<size=10>** {currentVersion} **</size>"},
            {"INVALID_TITLE", "EMPLACEMENT DE PROJET NON VALIDE"},
            {"MORE_INFO", "(Pour plus d'informations, consulter le message dans la console.)"},
            {"EXPLANATION_START", "Le projet est"},
            {"WHY", "C'est un problème, car ce dossier peut être synchronisé par OneDrive."},
            {"SOLUTION_MOVE", "Solution: 1. Quitter Unity ; 2. Déplacer le projet dans un autre dossier ; 3. Réouvrir le projet."},
            {"SOLUTION_RENAME", "Solution: 1. Quitter Unity ; 2. Éliminer les accents de la structure où se trouve le projet ; 3. Réouvrir le projet."},
            {"PATH_TOO_LONG", "dans un dossier dont le chemin est trop long ({0} caractères). Le chemin ne devrait pas dépasser {1} caractères."},
            {"ACCENTED_CHARACTERS", "dans un dossier dont le chemin contient des caractères accentués."},
            {"CLOUD_FOLDER", "dans un dossier {0}."},
            {"DOCUMENTS_FOLDER", "dans le dossier Documents de l'OS."},
            {"ON_DESKTOP", "sur le bureau."},
            {"CAUSE_PATH_TOO_LONG", "chemin trop long"},
            {"CAUSE_ACCENTED_CHARACTERS", "caractères accentués"},
            {"CONGRATS", "Bravo le projet est dans un dossier adéquat: chemin court, sans accent, pas dans un dossier OneDrive."},
            {"CURRENT_LOCATION", "Emplacement actuel:"}
        };

        // Constants for testing menus:
        private const string _MENU_TEST = "Help/Project Path Checker/Test Project Location %&t";
        private const string _MENU_CLEAR_WARNINGS = "Help/Project Path Checker/Clear Warnings %&#t";
        private const string _MENU_SIMULATE_DESKTOP = "Help/Project Path Checker/Simulate Project Location on Desktop"; // (Windows only)
        private const string _MENU_SIMULATE_DOCUMENTS = "Help/Project Path Checker/Simulate Project Location in Documents"; // (Windows only)
        private const string _MENU_SIMULATE_ONEDRIVE = "Help/Project Path Checker/Simulate Project Location in OneDrive";
        private const string _MENU_SIMULATE_ACCENTED_PATH = "Help/Project Path Checker/Simulate Project Location with Accented Characters";
        private const string _MENU_SIMULATE_LONG_PATH = "Help/Project Path Checker/Simulate Project Location with Long Path";
        private const string _MENU_CANCEL_SIMULATIONS = "Help/Project Path Checker/Cancel All Simulations";


        // The dictionary to use for messages, depending on the current language:
        static private Dictionary<string, string> _messages = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? _messagesFr : _messagesEn;

        private const int _MAX_PATH_LENGTH = 90; // Gives a margin of 170 characters for the path of files in the Library folder

        private static readonly List<string> _errorsList = new(); // Reminder: although readonly, the elements of the list can be modified
        private static readonly Color _boxColor = new(0.75f, 0f, 0f, 0.5f); // Dark red, semi-transparent

        private static string _path = null; // Will be used for the project path, including any simulations for testing

        private static bool _hasAlreadyDisplayedSuccess = false; // To avoid displaying the positive message more than once

        private static readonly bool _forceEnglishMessages = false; // To force the display of messages in English (for testing purposes)

        /// <summary>
        /// Constructor - Called when the script is loaded
        /// Calls the CheckProjectPath method to checks if the project is in a suitable location.
        /// Also registers the CheckProjectPath method to be called on play mode changes and project changes.
        /// </summary>
        static ProjectPathChecker()
        {
            if (_forceEnglishMessages) _messages = _messagesEn;
            EditorApplication.projectChanged += CheckProjectPath;
            EditorApplication.playModeStateChanged += CheckProjectPath;
            CheckProjectPath(); //Initial check!
        }

        /// <summary>
        /// Checks the project path if the project state is changed to play mode.
        /// Also allows to modify the path for testing purposes.
        /// </summary>
        /// <param name="state"></param>
        private static void CheckProjectPath(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;

            _path = GetProjectFolderPath();

            #region // For testing purposes, see the corresponding block at the bottom of the class for the menus
            if (Menu.GetChecked(_MENU_SIMULATE_DESKTOP) && Application.platform == RuntimePlatform.WindowsEditor)
            {
                _path = AddLastSeparatorIfNeeded(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            }
            else if (Menu.GetChecked(_MENU_SIMULATE_DOCUMENTS) && Application.platform == RuntimePlatform.WindowsEditor)
            {
                _path = AddLastSeparatorIfNeeded(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            }

            if (Menu.GetChecked(_MENU_SIMULATE_ONEDRIVE)) _path += AddLastSeparatorIfNeeded("OneDrive");
            if (Menu.GetChecked(_MENU_SIMULATE_ACCENTED_PATH)) _path += AddLastSeparatorIfNeeded("Cégep");
            if (Menu.GetChecked(_MENU_SIMULATE_LONG_PATH))
            {
                string hundredChar = "";
                for (int i = 0; i < 100; i++) hundredChar += i % 10; // Loop to add 100 characters to the string
                _path += AddLastSeparatorIfNeeded(hundredChar);
            }
            #endregion

            _path += AddLastSeparatorIfNeeded(GetProjectFolderName());
            CheckProjectPath(); // So we call the method that checks if the location is adequate
        }

        /// <summary>
        /// Checks if the project is in a suitable location.
        /// </summary>
        private static void CheckProjectPath()
        {
            if (SessionState.GetInt("hasShownPositiveMessage", 0) == 1) return; // If the user has seen the congratulations message (1), the test is not performed again
            if (_path == null) CheckProjectPath(PlayModeStateChange.EnteredPlayMode); // Required when the project is loaded
            int characterCount = _path.Length;
            _errorsList.Clear();

            // Checking the length of the path
            if (characterCount > _MAX_PATH_LENGTH)
            {
                _errorsList.Add(_messages["CAUSE_PATH_TOO_LONG"]);
                Debug.LogWarning($"{_messages["EXPLANATION_START"]} {string.Format(_messages["PATH_TOO_LONG"], characterCount, _MAX_PATH_LENGTH)} {_messages["SOLUTION_MOVE"]}");
            }
            if (CheckForAccentedCharacters(_path))
            {
                _errorsList.Add(_messages["CAUSE_ACCENTED_CHARACTERS"]);
                Debug.LogWarning($"{_messages["EXPLANATION_START"]} {_messages["ACCENTED_CHARACTERS"]} {_messages["SOLUTION_RENAME"]}");
            }

            // Checking for problematic folders
            string[] sensitiveFolders = { "OneDrive", "Dropbox", "Google", "apple~Cloud" };
            foreach (string folder in sensitiveFolders)
            {
                if (_path.IndexOf(folder, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    _errorsList.Add(string.Format(_messages["CLOUD_FOLDER"], folder));
                    Debug.LogWarning($"{_messages["EXPLANATION_START"]} {string.Format(_messages["CLOUD_FOLDER"], folder)} {_messages["SOLUTION_MOVE"]}");
                }
            }

            // Checking the Windows Documents and Desktop folders
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (_path.StartsWith(documentsPath, StringComparison.OrdinalIgnoreCase) && Application.platform == RuntimePlatform.WindowsEditor)
            {
                _errorsList.Add(_messages["DOCUMENTS_FOLDER"]);
                Debug.LogWarning($"{_messages["EXPLANATION_START"]} {_messages["DOCUMENTS_FOLDER"]} {_messages["WHY"]} {_messages["SOLUTION_MOVE"]}");
            }
            if (_path.StartsWith(desktopPath, StringComparison.OrdinalIgnoreCase) && Application.platform == RuntimePlatform.WindowsEditor)
            {
                _errorsList.Add(_messages["ON_DESKTOP"]);
                Debug.LogWarning($"{_messages["EXPLANATION_START"]} {_messages["ON_DESKTOP"]} {_messages["WHY"]}  {_messages["SOLUTION_MOVE"]}");
            }

            string coeur = "<color=red>♥</color>";
            if (_errorsList.Count > 0) DrawGizmo();
            else
            {
                if (_hasAlreadyDisplayedSuccess) return;
                if (_showPositiveMessages) Debug.Log($"<b><size=14> {coeur} {_messages["CONGRATS"]} {coeur} </size></b> \n{_messages["ABOUT"]}");
                _hasAlreadyDisplayedSuccess = true;
                SessionState.SetInt("hasShownPositiveMessage", 1);
            }
        }

        /// <summary>
        /// Find and returns the name of the current project folder.
        /// </summary>
        /// <returns>The folder name</returns>
        private static string GetProjectFolderName()
        {
            string projetPath = System.IO.Directory.GetCurrentDirectory();
            string projectFolderName = System.IO.Path.GetFileName(projetPath);
            return projectFolderName;
        }

        /// <summary>
        /// Allows to obtain the path where the project folder is located.
        /// </summary>
        /// <returns>The path of the project</returns>
        private static string GetProjectFolderPath()
        {
            string projetPath = System.IO.Directory.GetCurrentDirectory();
            int index = projetPath.LastIndexOf(Path.DirectorySeparatorChar);
            projetPath = projetPath[..(index + 1)]; //keeps the path, without the folder name
            return projetPath;
        }

        /// <summary>
        /// Adds a directory separator at the end of the path if it is not already there.
        /// </summary>
        /// <param name="path">The path to use</param>
        /// <returns>The path with the last separator</returns>
        private static string AddLastSeparatorIfNeeded(string path)
        {
            if (path[^1] != Path.DirectorySeparatorChar) path += Path.DirectorySeparatorChar;
            return path;
        }

        /// <summary>
        /// Draws a red gizmo in the scene view if the project is in an invalid location.
        /// </summary>
        private static void DrawGizmo()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>
        /// Displays the error message in the scene view.
        /// </summary>
        /// <param name="sceneView">The scene view</param>
        private static void OnSceneGUI(SceneView sceneView)
        {
            if (_errorsList.Count == 0) return; // If no error, nothing is displayed on the scene


            string path = _path ?? System.IO.Directory.GetCurrentDirectory(); // If it's a test, we use the test path, otherwise we use the real project path

            Handles.BeginGUI();

            // Getting the interface scaling factor (set by Unity and/or Windows preferences):
            float zoomFactor = EditorGUIUtility.pixelsPerPoint;
            // Getting the coordinates of the camera's viewport (without the scaling factor adjustment):
            Rect viewportRectAsIs = sceneView.camera.pixelRect;
            // Getting the adjusted coordinates of the viewport (taking into account the scaling factor):
            Rect viewportAjustedRect = new(viewportRectAsIs.position, viewportRectAsIs.size / zoomFactor);

            // Forcing Unity to redraw the viewport area (to avoid gizmos stacking up when doing multiple displays):
            Handles.DrawCamera(viewportAjustedRect, sceneView.camera); // To display the gizmos over the scene


            // A rectangle for displaying the text box (slightly smaller than the viewport, but centered):
            Rect boxRect = CreateRelativeRect(viewportAjustedRect, 0.025f, 0.025f, 0.95f, 0.95f);
            // A rectangle for displaying the title (half the height of the viewport, centered in the upper half of the viewport, same width - 10% as the viewport):
            Rect titleRect = CreateRelativeRect(viewportAjustedRect, 0.05f, 0.05f, 0.9f, 0.45f);
            // A rectangle for displaying the text (half the height of the viewport, centered in the lower half of the viewport, same width - 10% as the viewport):
            Rect textRect = CreateRelativeRect(viewportAjustedRect, 0.05f, 0.5f, 0.9f, 0.45f);

            Handles.DrawSolidRectangleWithOutline(boxRect, _boxColor, _boxColor); // To display the box that will contain the text

            // The style of the title:
            GUIStyle style = new(GUI.skin.label)
            {
                fontSize = (int)(viewportAjustedRect.width / 30), // fontSize of about 40 for a viewport 1200 pixels wide
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true // To avoid the text being cut off when a line is too long
            };

            // Creating the title label with the list of errors separated by commas:
            GUI.Label(titleRect, $"{_messages["INVALID_TITLE"]}\n({string.Join(", ", _errorsList)})", style);

            style.fontSize = style.fontSize * 60 / 100; // fontSize at 60% of the title size
            style.fontStyle = FontStyle.Normal;
            GUI.Label(textRect, $"{_messages["CURRENT_LOCATION"]} {path}\n\n{_messages["MORE_INFO"]}", style);

            Handles.EndGUI();
        }

        /// <summary>
        /// Creates a rectangle relative to another rectangle, based on factors for position and size.
        /// </summary>
        /// <param name="baseRect">The rect to use as a reference</param>
        /// <param name="posXFactor">The factor for the X position</param>
        /// <param name="posYFactor">The factor for the Y position</param>
        /// <param name="widthFactor">The factor for the width</param>
        /// <param name="heightFactor">The factor for the height</param>
        /// <returns>The new ajusted rectangle</returns>
        private static Rect CreateRelativeRect(Rect baseRect, float posXFactor, float posYFactor, float widthFactor, float heightFactor)
        {
            return new(baseRect.width * posXFactor, baseRect.height * posYFactor, baseRect.width * widthFactor, baseRect.height * heightFactor);
        }

        /// <summary>
        /// Checks if the input string contains accented characters.
        /// </summary>
        /// <param name="input">The string to check</param>
        /// <returns>True if the string contains accented characters, false otherwise</returns>
        private static bool CheckForAccentedCharacters(string input)
        {
            string pattern = @"[àáâãäåæçèéêëìíîïðñòóôõöøùúûüýþÿ]"; // a list of accented characters to check for
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase); // returns true if part of the string matches the pattern
        }


        ////////////////////////////////////////////////////////////////////////////

        #region // Menus to test the system
        // To force verification (with keyboard shortcut CTRL+Alt+T):
        [MenuItem(_MENU_TEST, false, 22)]
        private static void ForceProjectLocationCheck()
        {
            _errorsList.Clear();
            _hasAlreadyDisplayedSuccess = false;
            SessionState.SetInt("hasShownPositiveMessage", 0);
            OnSceneGUI(SceneView.lastActiveSceneView); // Force Unity to redraw the gizmos now
            CheckProjectPath(PlayModeStateChange.EnteredPlayMode);
            SceneView.RepaintAll(); // Force Unity to redraw the scene (again, to update the gizmos)
        }

        // To clear the error messages displayed on the scene (with keyboard shortcut CTRL+Alt+Shift+T):
        [MenuItem(_MENU_CLEAR_WARNINGS, false, 23)]
        private static void RemoveErrorMessages()
        {
            _errorsList.Clear();
            OnSceneGUI(SceneView.lastActiveSceneView); // Force Unity to redraw the gizmos now
        }

#if UNITY_EDITOR_WIN
        // To simulate that the project folder is on the Desktop (to test the error message)
        [MenuItem(_MENU_SIMULATE_DESKTOP)]
        private static void ToggleSimulateDesktopPath()
        {
            Menu.SetChecked(_MENU_SIMULATE_DESKTOP, !Menu.GetChecked(_MENU_SIMULATE_DESKTOP)); // Flips the menu state
            Menu.SetChecked(_MENU_SIMULATE_DOCUMENTS, false); // Disables the simulate Documents menu (it's one or the other)
        }


        // To simulate that the project folder is in Documents (to test the error message)
        [MenuItem(_MENU_SIMULATE_DOCUMENTS)]
        private static void ToggleSimulateDocumentsPath()
        {
            Menu.SetChecked(_MENU_SIMULATE_DOCUMENTS, !Menu.GetChecked(_MENU_SIMULATE_DOCUMENTS)); // Flips the menu state
            Menu.SetChecked(_MENU_SIMULATE_DESKTOP, false); // Disables the simulate Desktop menu (it's one or the other)
        }
#endif

        // To artificially add a OneDrive folder to the path (to test the error message)
        [MenuItem(_MENU_SIMULATE_ONEDRIVE)]
        private static void ToggleSimulateOnedrivePath()
        {
            Menu.SetChecked(_MENU_SIMULATE_ONEDRIVE, !Menu.GetChecked(_MENU_SIMULATE_ONEDRIVE)); // Flips the menu state
        }

        // To artificially add accents to the path (to test the error message)
        [MenuItem(_MENU_SIMULATE_ACCENTED_PATH)]
        private static void ToggleSimulateAccentPath()
        {
            Menu.SetChecked(_MENU_SIMULATE_ACCENTED_PATH, !Menu.GetChecked(_MENU_SIMULATE_ACCENTED_PATH)); // Flips the menu state
        }

        // To artificially lengthen the path (to test the error message)
        [MenuItem(_MENU_SIMULATE_LONG_PATH)]
        private static void ToggleSimulateLongPath()
        {
            Menu.SetChecked(_MENU_SIMULATE_LONG_PATH, !Menu.GetChecked(_MENU_SIMULATE_LONG_PATH)); // Flips the menu state
        }

        // To cancel all simulations (to test the real path)
        [MenuItem(_MENU_CANCEL_SIMULATIONS)]
        private static void UncheckAllSimulations()
        {
            Menu.SetChecked(_MENU_SIMULATE_DESKTOP, false);
            Menu.SetChecked(_MENU_SIMULATE_DOCUMENTS, false);
            Menu.SetChecked(_MENU_SIMULATE_ONEDRIVE, false);
            Menu.SetChecked(_MENU_SIMULATE_ACCENTED_PATH, false);
            Menu.SetChecked(_MENU_SIMULATE_LONG_PATH, false);
        }
        #endregion
    }
#endif
}