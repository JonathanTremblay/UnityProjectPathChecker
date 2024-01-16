using UnityEngine;
using UnityEditor;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

namespace ProjectPathChecker
{
    [InitializeOnLoad]
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
    public class ProjectPathChecker
    {
        static readonly string currentVersion = "Version 0.9.0 (2024-01)";

        private static readonly bool _showPositiveMessages = true; // If true, the positive message will be shown. If false, only the negative messages will be shown.

        private static readonly Dictionary<string, string> _messagesEn = new() // A dictionary for English messages
        {
            {"PRESENTATION", $"** Project Path Checker is free and open source. For updates and feedback, visit https://github.com/JonathanTremblay/UnityProjectPathChecker. **\n** {currentVersion} **"},
            {"TITRE_EMPLACEMENT_INVALIDE", "INVALID PROJECT LOCATION"},
            {"PLUS_INFOS", "(For more information, see the message in the console.)"},
            {"DEBUT_EXPLICATION", "The project is"},
            {"POURQUOI", "This is a problem because this folder can be synchronized by OneDrive."},
            {"SOLUTION_DEPLACER", "Solution: 1. Quit Unity ; 2. Move the project to another folder ; 3. Reopen the project."},
            {"SOLUTION_RENOMMER", "Solution: 1. Quit Unity ; 2. Remove accents from the structure where the project is located ; 3. Reopen the project."},
            {"CHEMIN_TROP_LONG", "in a folder whose path is too long ({0} characters). The path should not exceed {1} characters."},
            {"CARACTERES_ACCENTUES", "in a folder whose path contains accented characters."},
            {"DOSSIER_NUAGE", "in a {0} folder."},
            {"DOSSIER_DOCUMENTS", "in the OS's Documents folder."},
            {"SUR_BUREAU", "on the desktop."},
            {"CAUSE_CHEMIN_TROP_LONG", "path too long"},
            {"CAUSE_CARACTERES_ACCENTUES", "accented characters"},
            {"BRAVO", "Congratulations, the project is in a suitable folder: short path, no accents, not in a OneDrive folder."},
            {"EMPLACEMENT", "Current location:"}
        };

        private static readonly Dictionary<string, string> _messagesFr = new() // A dictionary for French messages
        {
            {"PRESENTATION", $"** Project Path Checker est gratuit et open source. Pour les mises à jour et les commentaires, visitez https://github.com/JonathanTremblay/UnityProjectPathChecker. **\n** {currentVersion} **"},
            {"TITRE_EMPLACEMENT_INVALIDE", "EMPLACEMENT DE PROJET NON VALIDE"},
            {"PLUS_INFOS", "(Pour plus d'informations, consulter le message dans la console.)"},
            {"DEBUT_EXPLICATION", "Le projet est"},
            {"POURQUOI", "C'est un problème, car ce dossier peut être synchronisé par OneDrive."},
            {"SOLUTION_DEPLACER", "Solution: 1. Quitter Unity ; 2. Déplacer le projet dans un autre dossier ; 3. Réouvrir le projet."},
            {"SOLUTION_RENOMMER", "Solution: 1. Quitter Unity ; 2. Éliminer les accents de la structure où se trouve le projet ; 3. Réouvrir le projet."},
            {"CHEMIN_TROP_LONG", "dans un dossier dont le chemin est trop long ({0} caractères). Le chemin ne devrait pas dépasser {1} caractères."},
            {"CARACTERES_ACCENTUES", "dans un dossier dont le chemin contient des caractères accentués."},
            {"DOSSIER_NUAGE", "dans un dossier {0}."},
            {"DOSSIER_DOCUMENTS", "dans le dossier Documents de l'OS."},
            {"SUR_BUREAU", "sur le bureau."},
            {"CAUSE_CHEMIN_TROP_LONG", "chemin trop long"},
            {"CAUSE_CARACTERES_ACCENTUES", "caractères accentués"},
            {"BRAVO", "Bravo le projet est dans un dossier adéquat: chemin court, sans accent, pas dans un dossier OneDrive."},
            {"EMPLACEMENT", "Emplacement actuel:"}
        };

        // Constants for testing menus:
        private const string _MENU_VERIF = "Window/Project Path Checker/Test Project Location %&t";
        private const string _MENU_EFFACER_VERIF = "Window/Project Path Checker/Clear Warnings %&#t";
        private const string _MENU_SIMULER_BUREAU = "Window/Project Path Checker/Simulate Project Location on Desktop"; // (Windows only)
        private const string _MENU_SIMULER_DOCUMENTS = "Window/Project Path Checker/Simulate Project Location in Documents"; // (Windows only)
        private const string _MENU_SIMULER_ONEDRIVE = "Window/Project Path Checker/Simulate Project Location in OneDrive";
        private const string _MENU_SIMULER_ACCENTS = "Window/Project Path Checker/Simulate Project Location with Accented Characters";
        private const string _MENU_SIMULER_CHEMIN_LONG = "Window/Project Path Checker/Simulate Project Location with Long Path";
        private const string _MENU_ANNULER_SIMULATIONS = "Window/Project Path Checker/Cancel All Simulations";


        // The dictionary to use for messages, depending on the current language:
        static private Dictionary<string, string> _messages = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? _messagesFr : _messagesEn;

        private const int _MAX_PATH_LENGTH = 90; // Gives a margin of 170 characters for the path of files in the Library folder

        private static readonly List<string> _lesErreurs = new(); // Reminder: although readonly, the elements of the list can be modified
        private static readonly Color _couleurDeLaBoite = new(0.75f, 0f, 0f, 0.5f); // Dark red, semi-transparent

        private static string _path = null; // Will be used for the project path, including any simulations for testing

        private static bool _aDejaAfficheBravo = false; // To avoid displaying the positive message more than once

        private static readonly bool _forceEnglishMessages = false; // To force the display of messages in English (for testing purposes)

        static ProjectPathChecker()
        {
            if (_forceEnglishMessages) _messages = _messagesEn;
            EditorApplication.projectChanged += CheckProjectPath;
            EditorApplication.playModeStateChanged += CheckProjectPath;
        }

        private static string ObtenirNomDossierProjet()
        {
            string pathDuProjet = System.IO.Directory.GetCurrentDirectory();
            string nomDossierProjet = System.IO.Path.GetFileName(pathDuProjet);
            return nomDossierProjet;
        }

        /// <summary>
        /// Allows to obtain the path where the project folder is located.
        /// </summary>
        /// <returnsThe path of the project</returns>
        private static string ObtenirCheminDossierProjet()
        {
            string pathDuProjet = System.IO.Directory.GetCurrentDirectory();
            int index = pathDuProjet.LastIndexOf(Path.DirectorySeparatorChar);
            pathDuProjet = pathDuProjet[..(index + 1)];
            return pathDuProjet;
        }

        private static void CheckProjectPath(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;

            _path = ObtenirCheminDossierProjet();

        #region // For testing purposes, see the corresponding block at the bottom of the class for the menus
        if (Menu.GetChecked(_MENU_SIMULER_BUREAU) && Application.platform == RuntimePlatform.WindowsEditor)
        {
            _path = AjouterSeparateurAuBesoin(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        }
        else if (Menu.GetChecked(_MENU_SIMULER_DOCUMENTS) && Application.platform == RuntimePlatform.WindowsEditor)
        {
            _path = AjouterSeparateurAuBesoin(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        }

        if (Menu.GetChecked(_MENU_SIMULER_ONEDRIVE)) _path += AjouterSeparateurAuBesoin("OneDrive");
        if (Menu.GetChecked(_MENU_SIMULER_ACCENTS)) _path += AjouterSeparateurAuBesoin("Cégep");
        if (Menu.GetChecked(_MENU_SIMULER_CHEMIN_LONG))
        {
            string cent = "";
            for (int i = 0; i < 100; i++) cent += i % 10; // Loop to add 100 characters to the string
            _path += AjouterSeparateurAuBesoin(cent);
        }
        #endregion

            _path += AjouterSeparateurAuBesoin(ObtenirNomDossierProjet());
            CheckProjectPath(); // So we call the method that checks if the location is adequate
        }

        private static string AjouterSeparateurAuBesoin(string path)
        {
            if (path[^1] != Path.DirectorySeparatorChar) path += Path.DirectorySeparatorChar;
            return path;
        }

        private static void CheckProjectPath()
        {
            if (SessionState.GetInt("hasShownPositiveMessage", 0) == 1) return; // If the user has seen the congratulations message (1), the test is not performed again
            if (_path == null) CheckProjectPath(PlayModeStateChange.EnteredPlayMode); // Required when the project is loaded
            int characterCount = _path.Length;
            _lesErreurs.Clear();

            // Checking the length of the path
            if (characterCount > _MAX_PATH_LENGTH)
            {
                _lesErreurs.Add(_messages["CAUSE_CHEMIN_TROP_LONG"]);
                Debug.LogWarning($"{_messages["DEBUT_EXPLICATION"]} {string.Format(_messages["CHEMIN_TROP_LONG"], characterCount, _MAX_PATH_LENGTH)} {_messages["SOLUTION_DEPLACER"]}");
            }
            if (VerifierSiContientAccents(_path))
            {
                _lesErreurs.Add(_messages["CAUSE_CARACTERES_ACCENTUES"]);
                Debug.LogWarning($"{_messages["DEBUT_EXPLICATION"]} {_messages["CARACTERES_ACCENTUES"]} {_messages["SOLUTION_RENOMMER"]}");
            }

            // Checking for problematic folders
            string[] sensitiveFolders = { "OneDrive", "Dropbox", "Google", "apple~Cloud" };
            foreach (string folder in sensitiveFolders)
            {
                if (_path.IndexOf(folder, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    _lesErreurs.Add(string.Format(_messages["DOSSIER_NUAGE"], folder));
                    Debug.LogWarning($"{_messages["DEBUT_EXPLICATION"]} {string.Format(_messages["DOSSIER_NUAGE"], folder)} {_messages["SOLUTION_DEPLACER"]}");
                }
            }

            // Checking the Windows Documents and Desktop folders
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (_path.StartsWith(documentsPath, StringComparison.OrdinalIgnoreCase) && Application.platform == RuntimePlatform.WindowsEditor)
            {
                _lesErreurs.Add(_messages["DOSSIER_DOCUMENTS"]);
                Debug.LogWarning($"{_messages["DEBUT_EXPLICATION"]} {_messages["DOSSIER_DOCUMENTS"]} {_messages["POURQUOI"]} {_messages["SOLUTION_DEPLACER"]}");
            }
            if (_path.StartsWith(desktopPath, StringComparison.OrdinalIgnoreCase) && Application.platform == RuntimePlatform.WindowsEditor)
            {
                _lesErreurs.Add(_messages["SUR_BUREAU"]);
                Debug.LogWarning($"{_messages["DEBUT_EXPLICATION"]} {_messages["SUR_BUREAU"]} {_messages["POURQUOI"]}  {_messages["SOLUTION_DEPLACER"]}");
            }

            string coeur = "<color=red>♥</color>";
            if (_lesErreurs.Count > 0) DrawGizmo();
            else
            {
                if (_aDejaAfficheBravo) return;
                if (_showPositiveMessages) Debug.Log($"<b><size=14> {coeur} {_messages["BRAVO"]} {coeur} </size></b> \n {_messages["PRESENTATION"]}");
                _aDejaAfficheBravo = true;
                SessionState.SetInt("hasShownPositiveMessage", 1);
            }
        }

        private static void DrawGizmo()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (_lesErreurs.Count == 0) return; // If no error, nothing is displayed on the scene


            string path = _path ?? System.IO.Directory.GetCurrentDirectory(); // If it's a test, we use the test path, otherwise we use the real project path

            Handles.BeginGUI();

            // Getting the interface scaling factor (set by Unity and/or Windows preferences):
            float facteurZoom = EditorGUIUtility.pixelsPerPoint;
            // Getting the coordinates of the camera's viewport (without the scaling factor adjustment):
            Rect rectDuViewportTelQuel = sceneView.camera.pixelRect;
            // Getting the adjusted coordinates of the viewport (taking into account the scaling factor):
            Rect rectDuViewport = new(rectDuViewportTelQuel.position, rectDuViewportTelQuel.size / facteurZoom);

            // Forcing Unity to redraw the viewport area (to avoid gizmos stacking up when doing multiple displays):
            Handles.DrawCamera(rectDuViewport, sceneView.camera); // To display the gizmos over the scene


            // A rectangle for displaying the text box (slightly smaller than the viewport, but centered):
            Rect rectDeLaBoite = CreerRectRelatif(rectDuViewport, 0.025f, 0.025f, 0.95f, 0.95f);
            // A rectangle for displaying the title (half the height of rectDeLaBoite, centered in the upper half of rectDeLaBoite, same width - 10% as the viewport):
            Rect rectDuTitre = CreerRectRelatif(rectDuViewport, 0.05f, 0.05f, 0.9f, 0.45f);
            // A rectangle for displaying the text (half the height of rectDeLaBoite, centered in the lower half of rectDeLaBoite, same width - 10% as the viewport):
            Rect rectDuTexte = CreerRectRelatif(rectDuViewport, 0.05f, 0.5f, 0.9f, 0.45f);

            Handles.DrawSolidRectangleWithOutline(rectDeLaBoite, _couleurDeLaBoite, _couleurDeLaBoite); // To display the box that will contain the text

            // The style of the title:
            GUIStyle style = new(GUI.skin.label)
            {
                fontSize = (int)(rectDuViewport.width / 30), // fontSize of about 40 for a viewport 1200 pixels wide
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true // To avoid the text being cut off when a line is too long
            };

            // Creating the title label with the list of errors separated by commas:
            GUI.Label(rectDuTitre, $"{_messages["TITRE_EMPLACEMENT_INVALIDE"]}\n({string.Join(", ", _lesErreurs)})", style);

            style.fontSize = style.fontSize * 60 / 100; // fontSize at 60% of the title size
            style.fontStyle = FontStyle.Normal;
            GUI.Label(rectDuTexte, $"{_messages["EMPLACEMENT"]} {path}\n\n{_messages["PLUS_INFOS"]}", style);

            Handles.EndGUI();
        }

        private static Rect CreerRectRelatif(Rect rectOri, float facteurPosX, float facteurPosY, float facteurTailleX, float facteurTailleY)
        {
            return new(rectOri.width * facteurPosX, rectOri.height * facteurPosY, rectOri.width * facteurTailleX, rectOri.height * facteurTailleY);
        }

        private static bool VerifierSiContientAccents(string input)
        {
            // Regular expression to detect special characters:
            string pattern = @"[àáâãäåæçèéêëìíîïðñòóôõöøùúûüýþÿ]";

            // Checking if the input contains special characters:
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
        }


        ////////////////////////////////////////////////////////////////////////////

        #region // Menus to test the system
        // To force verification (with keyboard shortcut CTRL+Alt+T):
        [MenuItem(_MENU_VERIF, false, 0)]
        private static void ForcerVerificationEmplacement()
        {
            _lesErreurs.Clear();
            _aDejaAfficheBravo = false;
            SessionState.SetInt("hasShownPositiveMessage", 0);
            OnSceneGUI(SceneView.lastActiveSceneView); // Force Unity to redraw the gizmos now
            CheckProjectPath(PlayModeStateChange.EnteredPlayMode);
            SceneView.RepaintAll(); // Force Unity to redraw the scene (again, to update the gizmos)
        }

        // To clear the error messages displayed on the scene (with keyboard shortcut CTRL+Alt+Shift+T):
        [MenuItem(_MENU_EFFACER_VERIF, false, 1)]
        private static void EffacerMessagesErreurs()
        {
            _lesErreurs.Clear();
            OnSceneGUI(SceneView.lastActiveSceneView); // Force Unity to redraw the gizmos now
        }

#if UNITY_EDITOR_WIN
    // To simulate that the project folder is on the Desktop (to test the error message)
    [MenuItem(_MENU_SIMULER_BUREAU)]
    private static void ToggleSimulerMauvaisEmplacementBureau()
    {
        Menu.SetChecked(_MENU_SIMULER_BUREAU, !Menu.GetChecked(_MENU_SIMULER_BUREAU)); // Flips the menu state
        Menu.SetChecked(_MENU_SIMULER_DOCUMENTS, false); // Disables the simulate Documents menu (it's one or the other)
    }


    // To simulate that the project folder is in Documents (to test the error message)
    [MenuItem(_MENU_SIMULER_DOCUMENTS)]
    private static void ToggleSimulerMauvaisEmplacementDocuments()
    {
        Menu.SetChecked(_MENU_SIMULER_DOCUMENTS, !Menu.GetChecked(_MENU_SIMULER_DOCUMENTS)); // Flips the menu state
        Menu.SetChecked(_MENU_SIMULER_BUREAU, false); // Disables the simulate Desktop menu (it's one or the other)
    }
#endif

        // To artificially add a OneDrive folder to the path (to test the error message)
        [MenuItem(_MENU_SIMULER_ONEDRIVE)]
        private static void ToggleSimulerMauvaisEmplacementOnedrive()
        {
            Menu.SetChecked(_MENU_SIMULER_ONEDRIVE, !Menu.GetChecked(_MENU_SIMULER_ONEDRIVE)); // Flips the menu state
        }

        // To artificially add accents to the path (to test the error message)
        [MenuItem(_MENU_SIMULER_ACCENTS)]
        private static void ToggleSimulerMauvaisEmplacementAccents()
        {
            Menu.SetChecked(_MENU_SIMULER_ACCENTS, !Menu.GetChecked(_MENU_SIMULER_ACCENTS)); // Flips the menu state
        }

        // To artificially lengthen the path (to test the error message)
        [MenuItem(_MENU_SIMULER_CHEMIN_LONG)]
        private static void ToggleSimulerMauvaisEmplacementLong()
        {
            Menu.SetChecked(_MENU_SIMULER_CHEMIN_LONG, !Menu.GetChecked(_MENU_SIMULER_CHEMIN_LONG)); // Flips the menu state
        }

        // To cancel all simulations (to test the real path)
        [MenuItem(_MENU_ANNULER_SIMULATIONS)]
        private static void DeselectionnerLesTests()
        {
            Menu.SetChecked(_MENU_SIMULER_BUREAU, false);
            Menu.SetChecked(_MENU_SIMULER_DOCUMENTS, false);
            Menu.SetChecked(_MENU_SIMULER_ONEDRIVE, false);
            Menu.SetChecked(_MENU_SIMULER_ACCENTS, false);
            Menu.SetChecked(_MENU_SIMULER_CHEMIN_LONG, false);
        }
        #endregion
    }
}