# Contribution Guidelines

This is the contribution guidelines file for the project.

## Foreword

Welcome to the ArrestManager+ project! This project is a continuation (of the services part) of the original Arrest Manager project made by Albo1125.

This project starts in early 2021, and as least until the end of the 2021, is being done by WithLithum *alone*.

While the project can go on with me alone but I always welcome contributions from the community because it will make the project achieve general availability faster.

Any sort of contribution will work. You can make suggestions for me (and possibly other users) to implement, or you can contribute code, or other kind of stuff.

Please note that this project does currently not accept finical contributions.

## Contribution Types

So these are the main types of contributions this project accept:

* Suggestions
* Bug Reports
* Feature Requests
* Implementation / fix of above three via MR

Please note that we do not accept bundling suggestions and feature requests / bug reports together with a MR. You'll have to create an issue first then create MR.

## Code Contribution

This section describes how to make code contribution.

### Setting up development environment

To build this project, you'll require:

* Visual Studio 2022 (community, as least)
  * with .NET Desktop development workload
* LSPD First Response (can be downloaded [here](https://www.lcpdfr.com/downloads/gta5mods/g17media/7792-lspd-first-response/))
* A genuine copy of Grand Theft Auto V

Follow the steps below to set up a development working directory for this project:

* Clone this repository. Run:
  
  ```sh
  git clone https://gitlab.com/WithLithum/arrestmanager.git
  ```

  Or you may do it via Visual Studio. If you forked the project you should clone your fork.
* Navigate to the cloned project via Explorer.
* Place these assemblies inside `Arrest Manager\dependencies`:
  * LSPD First Response (`LSPD First Response.dll`)
  * Albo1125.Common (`Albo1125.Common.dll`)
  * LSPDFR Computer+ (`ComputerPlus.dll`)
  * Better EMS (`BetterEMS.dll`)
* Add the NAL MyGet source to your NuGet, this can be done in Visual Studio:
  * Right-click the project and select "Manage NuGet packages"
  * Select the gear icon on the top-right corner of the NuGet pane.
  * Add this source: `https://www.myget.org/F/noartifactlights/api/v3/index.json`
  * Done.
* Perform a package restore.
* You should be ready to build the project.

After you've built the project you should be able to install the final binaries to your game directory.

### Creating a branch

To make a valid Merge Request, you must create a branch on your fork and commit changes there.

This can be done easily via Visual Studio.

### Creating a Merge Request

Anyone is allowed to create a merge request. However, before creating a merge request, please check:

* If your issue is reported - if not, report it first
* If your code style is complaint with rest of the project
* You have cleaned up your code

Please create appropriate comments for code review.

## Creating issue report

This section is to-do.

## Contact

You should contact me via the email on the GitLab profile or PM.
