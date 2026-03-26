# Aurora-LINK .NET 10 Cleanup & MSIX Modernization Tasks

## Overview

This document tracks the execution of the Aurora-LINK cleanup and MSIX modernization operation. All obsolete packaging infrastructure will be removed in a single atomic operation, transitioning to single-project MSIX packaging.

**Progress**: 1/2 tasks complete (50%) ![0%](https://progress-bar.xyz/50)

---

## Tasks

### [✓] TASK-001: Atomic cleanup and MSIX configuration *(Completed: 2026-03-26 09:57)*
**References**: Plan §Project-by-Project Plans, Plan §Migration Strategy

- [✓] (1) Remove `<Project Path="Aurora-LINK/Aurora-LINK (Package)/Aurora-LINK (Package).wapproj" ...>` from `Aurora-LINK.slnx`
- [✓] (2) Solution file updated successfully (**Verify**)
- [✓] (3) Delete `Aurora-LINK\Aurora-LINK (Package)\` directory from disk
- [✓] (4) WAP directory no longer exists (**Verify**)
- [✓] (5) Delete `Installer\` directory from disk
- [✓] (6) Installer directory no longer exists (**Verify**)
- [✓] (7) Delete `InstallerOutput\` directory from disk
- [✓] (8) InstallerOutput directory no longer exists (**Verify**)
- [✓] (9) Add MSIX image asset declarations to `Aurora-LINK\Aurora-LINK\Aurora-LINK.csproj` per Plan §Project 2 Step 1 (Content items with CopyToOutputDirectory for Images\*.png, *.jpg, *.ico)
- [✓] (10) Image asset declarations added successfully (**Verify**)
- [✓] (11) Remove `<None Include="Aurora-LINK %28Package%29_TemporaryKey.pfx" />` line from `Aurora-LINK\Aurora-LINK\Aurora-LINK.csproj` if present
- [✓] (12) Obsolete PFX reference removed (**Verify**)
- [✓] (13) Delete `Aurora-LINK\Aurora-LINK\Aurora-LINK (Package)_TemporaryKey.pfx` file if it exists
- [✓] (14) Orphaned PFX file removed if present (**Verify**)
- [✓] (15) Build solution in Release configuration
- [✓] (16) Solution builds with 0 errors (**Verify**)
- [✓] (17) Build MSIX package via `msbuild Aurora-LINK\Aurora-LINK\Aurora-LINK.csproj /t:Publish /p:Configuration=Release`
- [✓] (18) MSIX package generated successfully in output directory (**Verify**)

---

### [▶] TASK-002: Final commit
**References**: Plan §Source Control Strategy

- [▶] (1) Commit all changes with message: "chore: modernize to single-project MSIX packaging - Remove obsolete WAP project and Inno Setup installer infrastructure"

---










