using System;
using VVVV.PluginInterfaces.V2;
using System.ComponentModel.Composition;

namespace VVVV.Hosting
{
	[Export(typeof(IAddonFactory))]
	public class FreeFrameFactory : IAddonFactory
	{
		[ImportingConstructor]
		public FreeFrameFactory (INodeInfoFactory nodeInfoFactory)
		{
			nodeInfoFactory.NodeInfoAdded += HandleNodeInfoFactoryNodeInfoAdded;
		}

		void HandleNodeInfoFactoryNodeInfoAdded (object sender, INodeInfo nodeInfo)
		{
			if (nodeInfo.Type == NodeType.Freeframe)
				nodeInfo.Factory = this;
		}

		#region IAddonFactory implementation
		public System.Collections.Generic.IEnumerable<INodeInfo> ExtractNodeInfos (string filename, string arguments)
		{
			yield break;
		}

		public bool Create (INodeInfo nodeInfo, INode host)
		{
			return true;
		}

		public bool Delete (INodeInfo nodeInfo, INode host)
		{
			return true;
		}

		public bool Clone (INodeInfo nodeInfo, string path, string name, string category, string version, out INodeInfo newNodeInfo)
		{
			newNodeInfo = null;
			return false;
		}

		public void AddDir (string dir, bool recursive)
		{
			
		}

		public void RemoveDir (string dir)
		{
			
		}

		public string JobStdSubPath {
			get {
				return "freeframe";
			}
		}
		#endregion
	}
}

