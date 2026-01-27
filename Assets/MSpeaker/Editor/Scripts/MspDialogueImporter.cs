using System.IO;
using System.Text;
using MSpeaker.Runtime;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MSpeaker.Editor
{
    [ScriptedImporter(1, "msp")]
    public sealed class MspDialogueImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var dialogue = ScriptableObject.CreateInstance<MspDialogueAsset>();
            dialogue.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            dialogue.Content = File.ReadAllText(ctx.assetPath, Encoding.UTF8);

            ctx.AddObjectToAsset("Dialogue", dialogue);
            ctx.SetMainObject(dialogue);
        }
    }
}

