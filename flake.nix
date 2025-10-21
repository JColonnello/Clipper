{
  description = "A beautiful self-contained utility for clipping the best moments and uploading them to Discord";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs?ref=nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };
  outputs =
    {
      nixpkgs,
      flake-utils,
      ...
    }:
    flake-utils.lib.eachDefaultSystem (
      system:
      (
        let
          pkgs = (
            import nixpkgs {
              inherit system;
            }
          );
          lib = pkgs.lib;
          runtimePathDeps = [ pkgs.ffmpeg ];
        in
        {
          packages = {
            default = pkgs.buildDotnetModule {
              pname = "Clipper";
              version = "1.2";
              selfContainedBuild = true;

              src = ./.;

              projectFile = "./Clipper.csproj";

              makeWrapperArgs = [
                "--prefix"
                "PATH"
                ":"
                (lib.makeBinPath runtimePathDeps)
              ];
            };
          };
        }
      )
    );
}
