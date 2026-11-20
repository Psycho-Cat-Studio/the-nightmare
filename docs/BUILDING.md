# Building The Nightmare

## One-click builds (GitHub Actions)

The **Build game** workflow (`.github/workflows/build.yml`) builds Windows, macOS and Linux
packages with Unity 6000.6.7f1 through [GameCI](https://game.ci). Platforms build one after another
and import the project from scratch (a restored Library cache got the editor killed at start-up),
about 25 minutes each.

1. Open **Actions → Build game → Run workflow**.
2. Pick the platforms (default: all) and, optionally, tick *publish a GitHub Release*.
3. When it finishes, download the zip / tar.gz packages from the run's **Artifacts**
   (or from the pre-release, if you ticked it).

Pushing a tag such as `v1.1.0` builds all platforms and publishes a regular release.

Every pull request also runs **Compile check**: it compiles all scripts for Windows/macOS/Linux
plus the editor scripts with the Unity 6.6 compiler, and checks scene/prefab integrity and Mirror scene ids.
It needs no Unity license and takes a few minutes.

### One-time setup: Unity license secrets

Unity requires an activated license even for CI builds. Add these repository secrets
(**Settings → Secrets and variables → Actions → New repository secret**):

| Secret | Value |
| --- | --- |
| `UNITY_EMAIL` | Email of your Unity account |
| `UNITY_PASSWORD` | Password of your Unity account |
| `UNITY_LICENSE` | **Personal license:** the full contents of your `Unity_lic.ulf` file |
| `UNITY_SERIAL` | **Plus/Pro license only:** your serial key (instead of `UNITY_LICENSE`) |

To get `Unity_lic.ulf` for a Personal license, sign in to Unity Hub on your computer and activate
a Personal license (Hub → Settings → Licenses). The file is then at:

- Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
- macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
- Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`

See GameCI's [activation guide](https://game.ci/docs/github/activation) if Unity changes the flow.

The first build of each platform imports the whole project (~2.6 GB of assets) and takes a while;
the imported `Library` folder is cached, so later builds are much faster.

### Running the packages

- **Windows:** unzip and run `TheNightmare.exe`. Allow it through the firewall when asked
  (needed for hosting and LAN discovery).
- **macOS:** extract, then run `xattr -cr TheNightmare.app` once (the build is not signed/notarized)
  and open the app.
- **Linux:** extract and run `./TheNightmare`.

## Local builds

Open the project in Unity **6000.6.7f1** and use the menu **The Nightmare → Build**
(output goes to `Builds/<platform>/`). Building macOS/Linux from Windows requires the
corresponding Unity *Build Support* modules.

To check scripts without Unity installed, run the same compile check CI uses:

```bash
docker run --rm -v "$PWD:/project" -w /project unityci/editor:ubuntu-6000.6.7f1-base-3 bash .github/ci/compile-check.sh
```

## Playing together

Keep the existing Mirror stack for same-Wi-Fi play. See [NETWORKING.md](NETWORKING.md) for the
networking decision, troubleshooting and future relay/lobby options.

The Unity matchmaking/relay service the game originally used has been shut down. The game now
uses direct connections:

- **Host:** *Play → enter a room name → Create Room*. Your machine is the server (UDP port 7777).
  Your IP address is shown in the top-right corner in the pre-match lobby and pause menu (or press F1).
- **Join on the same network:** hosted games appear in the server list automatically
  (LAN discovery on UDP port 47777). Click one to join.
- **Join over the internet:** type the host's address in the *Host IP* field and press *Connect*.
  The host must either forward **UDP 7777** on their router or both players use a virtual LAN
  such as [Tailscale](https://tailscale.com) or [ZeroTier](https://zerotier.com) (then use the
  host's Tailscale/ZeroTier IP).
- Everyone must run the same build (the server list marks other versions). Joining is closed
  once the match has started.

Windows, macOS and Linux players can play together.

Command line (handy for testing two instances on one machine):

```bash
./TheNightmare -host -name "Test room" -port 7777
```

```bash
./TheNightmare -connect 127.0.0.1:7777
```

## Regression tests and module checks

In Unity Test Runner, select the `Nightmare.Tests` fixtures in EditMode and PlayMode. PlayMode
fixtures start real hosts on UDP 7798, unload scenes and return to MainMenu; stop your current
Play session first. EditMode covers rules/content; PlayMode covers actual gameplay integration.
No coverage percentage is prescribed.

With an open Editor and Unity CLI/Pipeline:

```bash
unity command test run --mode editor --filter Nightmare.Tests --project-path "$PWD"
unity command test run --mode playmode --async-tests true --filter Nightmare.Tests --project-path "$PWD"
unity command test status --project-path "$PWD"
python3 Tools/check_module_boundaries.py
python3 Tools/check_xml_docs.py
python3 Tools/check_code_hygiene.py
python3 Tools/assign_scene_ids.py --check
```

On SteamOS with a Flatpak Hub, forward these commands through the local `unity-steamos-flatpak`
skill helper; it discovers the current namespace instead of hard-coding a PID. Use
**The Nightmare → Validation → Validate game content** to check campaign bindings, catalog,
player rigs, missing scripts and stable scene IDs. Builds run this validation automatically.

For Editor-host + native-client regression, make a separate **development QA build** with
`ENABLE_RUNTIME_PIPELINE_DEV` temporarily set and all three normal build scenes. Restore the define
immediately after building; runtime Pipeline must stay out of normal release players. With the
Editor stopped in MainMenu, run:

```bash
python3 Tools/run_network_regression.py --player "$PWD/Builds/RefactorQA/TheNightmare"
```

The script starts/stops its own Play session, uses UDP 7799, launches the client and writes results
under `Logs/network-regression-<timestamp>/`. For Flatpak, pass
`--cli-prefix "flatpak enter <current-instance> /home/deck/.local/bin/unity"`; obtain the current
instance through the skill helper rather than copying an old number. It runs the QA player in the
same namespace. The harness requires Python 3 and a graphical Linux desktop. It refuses to replace
an already running Play session. Do not run multiple copies against one Editor or port.

The **Gameplay regression** GitHub workflow runs EditMode/PlayMode using the existing Unity license
secrets. It skips fork PRs and serializes with the build workflow. Compile checks remain
license-free; neither replaces two-process or real-device playtesting. Architecture and actual
verification evidence are in [REFACTORING.md](REFACTORING.md).
