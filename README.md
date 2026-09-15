<div class="header" align="center">
<img alt="Wayfarer Station" height="300" src="Resources/Textures/_WF/Logo/logo.png?raw=true" />
</div>

Wayfarer 14 is a fork of [Frontier Station](https://github.com/new-frontiers-14/frontier-station-14), which itself is a fork of [Space Station 14](https://github.com/space-wizards/space-station-14) that runs on [Robust Toolbox](https://github.com/space-wizards/RobustToolbox) engine written in C#.


This is the primary repo for Wayfarer 14.

If you want to host or create content for Wayfarer 14, this is the repo you need. It contains both RobustToolbox and the content pack for development of new content packs.

## Links

#### Wayfarer 14
<div class="header" align="center">

[Website](https://wayfarer14.com/) | [Discord](https://discord.gg/QpVfma2eAp) | [Wiki](https://wayfarer14.com/wiki)

</div>


#### Frontier Station
<div class="header" align="center">

[Discord](https://discord.com/invite/frontier) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/) | [Patreon](https://www.patreon.com/frontierstation14) | [Wiki](https://frontierstation.wiki.gg/)

</div>

#### Space Station 14
<div class="header" align="center">

[Website](https://spacestation14.io/) | [Discord](https://discord.ss14.io/) | [Forum](https://forum.spacestation14.io/) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/) | [Standalone Download](https://spacestation14.io/about/nightlies/)

</div>

## Contributing

We are happy to accept contributions from anybody. Get in Discord if you want to help. Don't be afraid to ask for help either!

We are not currently accepting translations of the game on our main repository. If you would like to translate the game into another language, consider creating a fork or contributing to a fork.

If you make any contributions, note that any changes made to files belonging to our upstream should be properly marked with comments (see the "Changes to upstream files" section in [CONTRIBUTING.md](CONTRIBUTING.md)).

## Building

1. Clone this repo:
```shell
git clone https://github.com/project-wayfarer/wayfarer-14.git
```
2. Go to the project folder and run `RUN_THIS.py` to initialize the submodules and load the engine:
```shell
cd wayfarer-14
python RUN_THIS.py
```
3. Compile the solution:

Build the server using `dotnet build`.

[More detailed instructions on building the project.](https://docs.spacestation14.com/en/general-development/setup.html)

## License

Read [LEGAL.md](LEGAL.md) for legal information regarding code licensing, including a table of attributions for each namespace within the codebase.

Most assets are licensed under CC-BY-SA 3.0 unless stated otherwise. Assets have their license and the copyright in the metadata file. Example.

Code taken from Emberfall was specifically relicensed under MIT terms with [permission from MilonPL](https://github.com/new-frontiers-14/frontier-station-14/pull/3607)

[2fca06eaba205ae6fe3aceb8ae2a0594f0effee0](https://github.com/project-wayfarer/wayfarer-14/commit/2fca06eaba205ae6fe3aceb8ae2a0594f0effee0) was pushed on July 1, 2024 at 16:04 UTC

Most assets are licensed under [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) unless stated otherwise. Assets have their license and copyright specified in the metadata file. For example, see the [metadata for a crowbar](Resources/Textures/Objects/Tools/crowbar.rsi/meta.json).

Note that some assets are licensed under the non-commercial [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/) or similar non-commercial licenses and will need to be removed if you wish to use this project commercially.
