#!/bin/bash
# Compile-checks all the game's Unity-side code (runtime scripts, editor scripts, tests) against the editor's own libraries,
# without opening Unity. It only finds compile errors, it cannot tell you the code works in the editor.
# Needs Git Bash (or any bash) and a project that Unity has compiled at least once (it reads Library/ScriptAssemblies).
# Usage: Tools/check-unity-compile.sh [max lines of output]   (prints "check done" and nothing else when everything compiles)
# Set UNITY_DATA to the editor's Data folder if Unity is not installed at the default place.
UNITY_DATA="${UNITY_DATA:-C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Data}"
M="$UNITY_DATA/MonoBleedingEdge/lib/mono/4.7.1-api"
U="$UNITY_DATA"
SA="Library/ScriptAssemblies"
cd "$(dirname "$0")/.." || exit 1
mkdir -p Tools/TestRunner/bin

refs="-r:\"$M/mscorlib.dll\" -r:\"$M/System.dll\" -r:\"$M/System.Core.dll\" -r:\"$M/Facades/netstandard.dll\" -r:\"$M/Facades/System.Runtime.dll\""
for f in UnityEngine.CoreModule UnityEngine UnityEngine.IMGUIModule UnityEngine.ImageConversionModule UnityEngine.UIModule UnityEngine.TextRenderingModule UnityEngine.PhysicsModule UnityEngine.Physics2DModule UnityEngine.AudioModule UnityEngine.InputLegacyModule UnityEngine.AnimationModule UnityEngine.JSONSerializeModule UnityEditor.CoreModule UnityEditor; do
  [ -f "$U/Managed/UnityEngine/$f.dll" ] && refs="$refs -r:\"$U/Managed/UnityEngine/$f.dll\""
done
for a in UnityEngine.UI Unity.TextMeshPro UniTask UniTask.DOTween LeanTouch Zenject; do
  refs="$refs -r:\"$SA/$a.dll\""
done
refs="$refs -r:\"Assets/Plugins/Demigiant/DOTween/DOTween.dll\""

files=$(ls Assets/Scripts/Board/*.cs Assets/Scripts/Board/Battle/*.cs Assets/Scripts/Board/Sim/*.cs Assets/Scripts/Board/Progress/*.cs Assets/Scripts/Campaign/*.cs Assets/Scripts/Campaign/Views/*.cs Assets/Scripts/BoardAuthoring/*.cs Assets/Scripts/Encounters/*.cs Assets/Scripts/BattleView/*.cs Assets/Scripts/Combat/EnemyAction.cs Assets/Scripts/Combat/EnemyActionSelectionMode.cs Assets/Scripts/Combat/EnemyDefinition.cs Assets/Scripts/Configurations/Pilots/PilotTokenConfiguration.cs Assets/Scripts/Configurations/GameRules.cs Assets/Scripts/Configurations/PilotDeck.cs Assets/Plugins/Demigiant/DOTween/Modules/*.cs Assets/Editor/*.cs 2>/dev/null | tr '\n' ' ')
eval "\"$U/NetCoreRuntime/dotnet.exe\" \"$U/DotNetSdkRoslyn/csc.dll\" -nologo -target:library -out:Tools/TestRunner/bin/unity_check.dll -langversion:9 -nowarn:CS0649,CS0414,CS0169,CS8632,CS1701,CS1702 $refs $files" 2>&1 | grep -v "warning" | head -${1:-30}
echo "check done"
