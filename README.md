# AutoVersioning for Unity

An asset for Unity to automatically update the version number using Git.

It was created with the goal of automatically giving consistent version numbers based on Git history, regardless of whether it is a remote build (e.g. GitLab CI) or a local build.

Note that this asset uses the paid asset Odin Inspector as an editor extension, so you will need to either install Odin Inspector or modify this code to not use it.

# Install
Requires Unity 2020.3+ and Odin Inspector

1. Open Window > Package Manager
2. Press the + button in the upper left corner
3. Select "Add package from git URL..."
4. Enter `https://github.com/Skyzi000/AutoVersioning-for-Unity.git`
