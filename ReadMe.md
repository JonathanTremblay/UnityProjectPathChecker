# Unity ProjectPathChecker

This Editor script checks the project's location for optimal Unity operation.

* The project must not be on the Desktop, in the Documents folder or in a folder synchronized by OneDrive or other synchronization service.
* The project path must not contain accents and it must be short.

## Table of Contents

1. [Getting Started](#getting-started)
2. [Technical Details](#technical-details)
3. [Compatibility](#compatibility)
4. [Known Issues](#known-issues)
5. [About the Project](#about-the-project)
6. [Contact](#contact)
7. [Version History](#version-history)
8. [License](#license)

## Getting Started

* Import this lightweight package to your project (or manually add the scripts to an Editor folder in the Assets folder).
* To use it, simply open your project.
* It executes the verification only once, when the Unity editor is loaded or when the script is added for the first time.
* If the project is in an invalid location, an error message is displayed in the console and a red gizmo is displayed directly in the scene.
* That's it!

## Technical Details

* This script is compatible with Windows and MacOS.
* For testing purposes, all results may be simulated (see Window/Project Path Checker/).

## Compatibility

* Tested on Windows and MacOS with Unity version 2022.3.17 (LTS).

## Known Issues

* On MacOS, messages are always displayed in English. (On Windows, if the language settings are in French, messages are displayed in French.)
* (Issues can be reported on GitHub: https://github.com/JonathanTremblay/UnityProjectPathChecker/issues)

## About the Project

* I created this tool to help my students form good habits when it comes to the placement of their Unity projects.

## Contact

**Jonathan Tremblay**  
Teacher, Cegep de Saint-Jerome  
jtrembla@cstj.qc.ca

Project Repository: https://github.com/JonathanTremblay/UnityProjectPathChecker

## Version History

* 0.9.0
    * First public version.

## License

This tool is available for distribution and modification under the CC0 License, which allows for free use and modification.  
https://creativecommons.org/share-your-work/public-domain/cc0/