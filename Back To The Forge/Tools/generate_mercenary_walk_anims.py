import hashlib
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "Assets" / "Sprites" / "Mercenaries"
COMBAT = Path(__file__).resolve().parents[1] / "Assets" / "CombatData"

MERCENARIES = ["Rook", "Brynja", "Kaela", "Silas", "Mira", "Tomas", "Vex"]

ANIM_CLIP = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {clip_name}
  serializedVersion: 7
  m_Legacy: 0
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves: []
  m_PositionCurves: []
  m_ScaleCurves: []
  m_FloatCurves: []
  m_PPtrCurves:
  - serializedVersion: 2
    curve:
    - time: 0
      value: {{fileID: {id0}, guid: {sheet_guid}, type: 3}}
    - time: 0.083333336
      value: {{fileID: {id1}, guid: {sheet_guid}, type: 3}}
    attribute: m_Sprite
    path: 
    classID: 212
    script: {{fileID: 0}}
    flags: 2
  m_SampleRate: 12
  m_WrapMode: 0
  m_Bounds:
    m_Center: {{x: 0, y: 0, z: 0}}
    m_Extent: {{x: 0, y: 0, z: 0}}
  m_ClipBindingConstant:
    genericBindings:
    - serializedVersion: 2
      path: 0
      attribute: 0
      script: {{fileID: 0}}
      typeID: 212
      customType: 23
      isPPtrCurve: 1
      isIntCurve: 0
      isSerializeReferenceCurve: 0
    pptrCurveMapping:
    - {{fileID: {id0}, guid: {sheet_guid}, type: 3}}
    - {{fileID: {id1}, guid: {sheet_guid}, type: 3}}
  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {{fileID: 0}}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: 0.16666667
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: 1
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 0
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 0
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves: []
  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
"""

ANIM_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 7400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

CTRL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1107 &-6149563903889374190
AnimatorStateMachine:
  serializedVersion: 7
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Base Layer
  m_ChildStates:
  - serializedVersion: 1
    m_State: {{fileID: 7848270421328794675}}
    m_Position: {{x: 285, y: 120, z: 0}}
  - serializedVersion: 1
    m_State: {{fileID: 8541644076234016629}}
    m_Position: {{x: 285, y: 240, z: 0}}
  - serializedVersion: 1
    m_State: {{fileID: -974147251370879260}}
    m_Position: {{x: 500, y: 180, z: 0}}
  - serializedVersion: 1
    m_State: {{fileID: 5123456789012345678}}
    m_Position: {{x: 70, y: 180, z: 0}}
  m_ChildStateMachines: []
  m_AnyStateTransitions: []
  m_EntryTransitions: []
  m_StateMachineTransitions: {{}}
  m_StateMachineBehaviours: []
  m_AnyStatePosition: {{x: 50, y: 20, z: 0}}
  m_EntryPosition: {{x: 50, y: 120, z: 0}}
  m_ExitPosition: {{x: 800, y: 120, z: 0}}
  m_ParentStateMachinePosition: {{x: 800, y: 20, z: 0}}
  m_DefaultState: {{fileID: 7848270421328794675}}
--- !u!1102 &-974147251370879260
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Left
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions: []
  m_StateMachineBehaviours: []
  m_Position: {{x: 50, y: 50, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {left_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!91 &9100000
AnimatorController:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}Walk
  serializedVersion: 6
  m_AnimatorParameters: []
  m_AnimatorLayers:
  - serializedVersion: 5
    m_Name: Base Layer
    m_StateMachine: {{fileID: -6149563903889374190}}
    m_Mask: {{fileID: 0}}
    m_Motions: []
    m_Behaviours: []
    m_BlendingMode: 0
    m_SyncedLayerIndex: -1
    m_DefaultWeight: 0
    m_IKPass: 0
    m_SyncedLayerAffectsTiming: 0
    m_Controller: {{fileID: 9100000}}
  m_EvaluateTransitionsOnStart: 1
--- !u!1102 &7848270421328794675
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Down
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions: []
  m_StateMachineBehaviours: []
  m_Position: {{x: 50, y: 50, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {down_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!1102 &8541644076234016629
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Up
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions: []
  m_StateMachineBehaviours: []
  m_Position: {{x: 50, y: 50, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {up_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!1102 &5123456789012345678
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Right
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions: []
  m_StateMachineBehaviours: []
  m_Position: {{x: 50, y: 50, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {right_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
"""

CTRL_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 9100000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def guid_for(name, kind):
    return hashlib.md5(f"merc-walk-v1-{name}-{kind}".encode()).hexdigest()


def parse_meta(meta_path):
    text = meta_path.read_text(encoding="utf-8")
    sheet_guid = re.search(r"^guid: (\w+)$", text, re.M).group(1)
    ids = {}
    for m in re.finditer(r"^\s+(\w+_Walk_Spritesheet_(\d+)): (-?\d+)$", text, re.M):
        ids[int(m.group(2))] = int(m.group(3))
    rows = {}
    for m in re.finditer(
        r"name: \w+_Walk_Spritesheet_(\d+)\s+rect:\s+serializedVersion: 2\s+x: \d+\s+y: (\d+)",
        text,
    ):
        idx = int(m.group(1))
        y = int(m.group(2))
        rows.setdefault(y, []).append(idx)
    ordered_rows = [sorted(rows[y]) for y in sorted(rows.keys(), reverse=True)]
    return sheet_guid, ids, ordered_rows


def pick_pair(indices):
    if len(indices) >= 3:
        return indices[0], indices[-1]
    if len(indices) == 2:
        return indices[0], indices[1]
    if len(indices) == 1:
        return indices[0], indices[0]
    raise ValueError("empty row")


def map_directions(ids, cols=3):
    indices = sorted(ids.keys())
    if len(indices) < 12:
        raise ValueError(f"Expected at least 12 sliced sprites, got {len(indices)}")

    rows = [indices[i : i + cols] for i in range(0, len(indices), cols)]
    if len(rows) < 4:
        raise ValueError(f"Expected at least 4 rows, got {len(rows)}")

    # Sheet order top-to-bottom: Down, Right, Left, …Up on last row.
    return (
        pick_pair(rows[0]),
        pick_pair(rows[1]),
        pick_pair(rows[2]),
        pick_pair(rows[-1]),
    )


def write_anim(folder, clip, content, guid):
    p = folder / f"{clip}.anim"
    p.write_text(content, encoding="utf-8")
    (folder / f"{clip}.anim.meta").write_text(ANIM_META.format(guid=guid), encoding="utf-8")


def update_hire_offer(name, ctrl_guid):
    asset = COMBAT / f"HireOffer_{name}.asset"
    text = asset.read_text(encoding="utf-8")
    line = f"  walkAnimatorController: {{fileID: 9100000, guid: {ctrl_guid}, type: 2}}"
    if "walkAnimatorController:" in text:
        text = re.sub(r"  walkAnimatorController:.*", line, text)
    else:
        text = text.replace("  battleReadySprite:", line + "\n  battleReadySprite:")
    asset.write_text(text, encoding="utf-8")


def main():
    for name in MERCENARIES:
        folder = ROOT / name
        meta = folder / f"{name}_Walk_Spritesheet.png.meta"
        sheet_guid, ids, _ordered_rows = parse_meta(meta)
        down, right, left, up = map_directions(ids)

        guids = {c: guid_for(name, c) for c in ["Down", "Right", "Left", "Up", "Controller"]}

        for clip, pair in [("Down", down), ("Right", right), ("Left", left), ("Up", up)]:
            content = ANIM_CLIP.format(
                clip_name=clip,
                id0=ids[pair[0]],
                id1=ids[pair[1]],
                sheet_guid=sheet_guid,
            )
            write_anim(folder, clip, content, guids[clip])

        ctrl_path = folder / f"{name}Walk.controller"
        ctrl_path.write_text(
            CTRL.format(
                name=name,
                down_guid=guids["Down"],
                right_guid=guids["Right"],
                left_guid=guids["Left"],
                up_guid=guids["Up"],
            ),
            encoding="utf-8",
        )
        (folder / f"{name}Walk.controller.meta").write_text(
            CTRL_META.format(guid=guids["Controller"]), encoding="utf-8"
        )

        update_hire_offer(name, guids["Controller"])
        print(
            f"{name}: sprites={len(ids)} down={down} right={right} left={left} up={up} ctrl={guids['Controller']}"
        )


if __name__ == "__main__":
    main()
