﻿#region usings
using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using VVVV.Core.Logging;

using OpenNI;
#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "Kinect",
	            Category = "Devices",
	            Version = "OpenNI",
	            Help = "Provides access to a Kinect through the OpenNI API",
	            Author = "Phlegma")]
	#endregion PluginInfo
	public class Configuration: IPluginEvaluate, IDisposable
	{
		#region fields & pins

		//vvvv
		[Input("Configuration File", DefaultString = "OpenNIConfig.xml", StringType = StringType.Filename, IsSingle=true)]
		IDiffSpread<string> FConfigPinIn;

		[Input("Reload Configuration", IsSingle = true, IsBang = true)]
		IDiffSpread<bool> FReloadIn;
		
		[Input("Mirrored", IsSingle = true, DefaultValue = 1)]
        ISpread<bool> FMirrored;

		[Input("Enabled", IsSingle = true, DefaultValue = 1)]
		IDiffSpread<bool> FUpdateIn;

		[Output("Context")]
		ISpread<Context> FContextOut;

		[Output("Default Values")]
		ISpread<bool> FDefaultValuesOut;

		[Output("Node List")]
		ISpread<string> FNodeListOut;

		[Output("Status")]
		ISpread<string> FStatus;

		[Import()]
		ILogger FLogger;

		//Kinect
		private Context FContext;
		private List<string> FErrors = new List<string>();
		private Thread FUpdater;
		private bool FInit = true;
		private bool FActiveThread = false;
		#endregion fields & pins

		#region Evaluate

		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			//Start
			if (FInit)
			{
				FStatus[0] = "Init OpenNI Node.";

				//Init the Context Object
				LoadContext(FConfigPinIn[0]);
				
				//Thread for updating the Generators
				FActiveThread = true;
				FUpdater = new Thread(Update);
				FUpdater.Start();
			}
			else
			{
				//check if Filepath or Relaod Pin is changed
				if (FReloadIn.IsChanged || FConfigPinIn.IsChanged)
				{
					if (FContext != null)
					{
						FContextOut[0] = null;
						CloseContext();
					}
					FInit = true;
				}

				if (FContext != null)
				{
					//Spits out the Global Error Massage
					if (!String.IsNullOrEmpty(FContext.GlobalErrorState))
					{
						FStatus[0] = FContext.GlobalErrorState;
					}

					//Close and restart the Update Thread
					if (FUpdateIn.IsChanged)
					{
						if (FUpdateIn[0])
						{
							if (FUpdater == null)
							{
								FActiveThread = true;
								FUpdater = new Thread(Update);
								FUpdater.Start();
							}
						}
						else
						{
							FActiveThread = false;
							FUpdater = null;
						}
					}
					
					FContext.GlobalMirror = FMirrored[0];
				}

				//writes the Context Object to the Output for
				//is required for other generators
				FContextOut[0] = FContext;
			}
		}

		#endregion

		#region Open and close Context

		/// <summary>
		/// Aborts the Update Thread and disposes the Context Object
		/// </summary>
		private void CloseContext()
		{
			if (FUpdater.IsAlive)
			{
				FActiveThread = false;
				//wait for threadloop to exit
				FUpdater.Join();
			}

			if (FContext != null)
			{
				//FContext.StopGeneratingAll();
				FContext.Release();
				FContext.Dispose();
				FContext = null;
			}
		}

		/// <summary>
		/// Load the Context XML from File or from the OpenNI Libary
		/// </summary>
		/// <param name="FilePath"></param>
		private void LoadContext(string FilePath)
		{
			//ty to open Kinect Context with given ConfigFilePath
			try
			{
				ScriptNode Node;
				//OpenNI.Log.SetSeverityFilter(LogSeverity.Error);
				FContext = new Context(FConfigPinIn[0]);
				var version = OpenNI.Version.Current.Major.ToString() + "." + OpenNI.Version.Current.Minor.ToString() + "." + OpenNI.Version.Current.Maintenance.ToString() + "." + OpenNI.Version.Current.Build.ToString();
				FLogger.Log(LogType.Message, "OpenNI Version: " + version);
				
				FDefaultValuesOut[0] = false;
			}
			catch (StatusException ex)
			{
				FLogger.Log(ex);
			}
			catch (GeneralException e)
			{
				FLogger.Log(e);
			}

			//try to OpenOpenNi Cofig File
			if (FContext == null)
			{
				FStatus[0] = "Error loading SampleConfig.xml. Try to load Default Values.";
				try
				{
					FContext = new Context();
					FDefaultValuesOut[0] = true;
				}
				catch (StatusException ex)
				{
					FLogger.Log(ex);
				}
				catch (GeneralException e)
				{
					FLogger.Log(e);
				}
			}

			if (FContext == null)
				FStatus[0] = "Could no create Context.";
			else
			{
				FContext.ErrorStateChanged += new EventHandler<ErrorStateEventArgs>(FContext_ErrorStateChanged);

				FStatus[0] = "Created Context.";

				//write all found Nodes in the config xml to the Output Pin
				List<string> NodeNames = ReadNodeList();
				FNodeListOut.SliceCount = NodeNames.Count;
				for (int i = 0; i < NodeNames.Count; i++)
				{
					FNodeListOut[i] = NodeNames[i];
				}

				FInit = false;
			}
		}

		#endregion

		#region Error Event

		/// <summary>
		/// callback Function for the Error Changed Event
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void FContext_ErrorStateChanged(object sender, ErrorStateEventArgs e)
		{
			FLogger.Log(LogType.Error,"Global Kinect Error");
		}

		/// <summary>
		/// Reads all nodes form the Config XML file
		/// </summary>
		/// <returns>List of all Nodes</returns>
		private List<string> ReadNodeList()
		{
			NodeInfoList NodeInfos = FContext.EnumerateExistingNodes();
			IEnumerator<NodeInfo> Infos = NodeInfos.GetEnumerator();

			List<string> NodeNames = new List<string>();
			while (Infos.MoveNext())
			{
				NodeNames.Add(Infos.Current.InstanceName);
			}

			return NodeNames;
		}

		#endregion

		#region Update Thread
		/// <summary>
		/// Thread for updating all Generators
		/// </summary>
		private void Update()
		{
			while (FUpdateIn[0] && FInit == false && FActiveThread)
			{
				try
				{
					//The way how to update
					if (FContext != null)
						FContext.WaitAnyUpdateAll();
				}
				catch (StatusException ey)
				{
					Debug.WriteLine(ey.Message);
				}
				catch (GeneralException ez)
				{
					Debug.WriteLine(ez.Message);
				}
				catch (AccessViolationException ex)
				{
					Debug.WriteLine(ex.Message);
				}
			}
		}

		#endregion

		#region Dispose

		public void Dispose()
		{
			CloseContext();
		}

		#endregion Dispose
	}
}
