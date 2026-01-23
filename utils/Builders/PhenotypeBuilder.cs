using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NeoModLoader.utils.Builders
{
    /// <summary>
    /// Phenotype asset builder
    /// </summary>
    public class PhenotypeBuilder : UnlockableAssetBuilder<PhenotypeAsset, PhenotypeLibrary>
    {
        /// <inheritdoc/>
        public PhenotypeBuilder(string ID) : base(ID)
        {
        }
        /// <inheritdoc/>
        public PhenotypeBuilder(string FilePath, bool LoadImmediately) : base(FilePath, LoadImmediately)
        {
        }
        /// <inheritdoc/>
        public PhenotypeBuilder(string ID, string CopyFrom) : base(ID, CopyFrom)
        {
        }
        /// <inheritdoc/>
        public override void Build(bool LinkWithOtherAssets)
        {
            base.Build(LinkWithOtherAssets);
        }
        /// <inheritdoc/>
        public override void LinkAssets()
        {
            Library.createShades(Asset);
            Asset.phenotype_index = Library.list.Count-1;
            Library._phenotypes_assets_by_index.Add(Asset.phenotype_index, Asset);
            base.LinkAssets();
        }
        /// <summary>
        /// the first of its shades
        /// </summary>
        public Color FirstShade { get { return Toolbox.makeColor(Asset.shades_from); } set { Asset.shades_from = Toolbox.colorToHex(value); } }
        /// <summary>
        /// the last of its shades, the game automatically creates a range of colors from first to last
        /// </summary>
        public Color LastShade { get { return Toolbox.makeColor(Asset.shades_to); } set { Asset.shades_to = Toolbox.colorToHex(value); } }
    }
}
