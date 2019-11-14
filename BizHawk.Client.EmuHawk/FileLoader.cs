﻿using System;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;

using BizHawk.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;
using BizHawk.Client.Common;

namespace BizHawk.Client.EmuHawk
{
	partial class MainForm
	{
		private enum LoadOrdering
		{
			Rom,
			State,
			Watch,
			CdFile,
			LuaSession,
			LuaScript,
			Cheat,
			MovieFile,
			LegacyMovieFile
		}

		public struct FileInformation
		{
			public string DirectoryName { get; }
			public string FileName { get; }
			public string ArchiveName { get; }

			public FileInformation(string directory, string file, string archive)
			{
				DirectoryName = directory;
				FileName = file;
				ArchiveName = archive;
			}
		}

		private IEnumerable<string> KnownRomExtensions =>
			RomFilterEntries.SelectMany(f => f.EffectiveFilters.Where(s => s.StartsWith("*.", StringComparison.Ordinal)).Select(s => s.Substring(1).ToUpperInvariant()));

		private readonly string[] _nonArchive = { ".ISO", ".CUE", ".CCD" };

		#region Loaders

		private void LoadCdl(string filename, string archive = null)
		{
			if (GlobalWin.Tools.IsAvailable<CDL>())
			{
				CDL cdl = GlobalWin.Tools.Load<CDL>();
				cdl.LoadFile(filename);
			}
		}

		private void LoadCheats(string filename, string archive = null)
		{
			Global.CheatList.Load(filename, false);
			GlobalWin.Tools.Load<Cheats>();
		}

		private void LoadLegacyMovie(string filename, string archive = null)
		{
			if (Global.Emulator.IsNull())
			{
				OpenRom();
			}

			if (Global.Emulator.IsNull())
			{
				return;
			}

			// tries to open a legacy movie format by importing it
			var movie = MovieImport.ImportFile(filename, out var errorMsg, out var warningMsg);
			if (!string.IsNullOrEmpty(errorMsg))
			{
				MessageBox.Show(errorMsg, "Conversion error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			else
			{
				// Fix movie extension to something palatable for these purposes. 
				// For instance, something which doesn't clobber movies you already may have had.
				// I'm evenly torn between this, and a file in %TEMP%, but since we don't really have a way to clean up this tempfile, I choose this:
				StartNewMovie(movie, false);
			}

			GlobalWin.OSD.AddMessage(warningMsg);
		}

		private void LoadLuaFile(string filename, string archive = null)
		{
			OpenLuaConsole();
			if (GlobalWin.Tools.Has<LuaConsole>())
			{
				GlobalWin.Tools.LuaConsole.LoadLuaFile(filename);
			}
		}

		private void LoadLuaSession(string filename, string archive = null)
		{
			OpenLuaConsole();
			if (GlobalWin.Tools.Has<LuaConsole>())
			{
				GlobalWin.Tools.LuaConsole.LoadLuaSession(filename);
			}
		}

		private void LoadMovie(string filename, string archive = null)
		{
			if (Global.Emulator.IsNull())
			{
				OpenRom();
			}

			if (Global.Emulator.IsNull())
			{
				return;
			}

			StartNewMovie(MovieService.Get(filename), false);
		}

		private void LoadRom(string filename, string archive = null)
		{
			var args = new LoadRomArgs
			{
				OpenAdvanced = new OpenAdvanced_OpenRom {Path = filename}
			};
			LoadRom(filename, args);
		}

		private void LoadStateFile(string filename, string archive = null)
		{
			LoadState(filename, Path.GetFileName(filename));
		}

		private void LoadWatch(string filename, string archive = null)
		{
			GlobalWin.Tools.LoadRamWatch(true);
			((RamWatch) GlobalWin.Tools.Get<RamWatch>()).LoadWatchFile(new FileInfo(filename), false);
		}

		#endregion

		private void ProcessFileList(IEnumerable<string> fileList, ref Dictionary<LoadOrdering, List<FileInformation>> sortedFiles, string archive = null)
		{
			foreach (string file in fileList)
			{
				var ext = Path.GetExtension(file)?.ToUpperInvariant() ?? "";
				FileInformation fileInformation = new FileInformation(Path.GetDirectoryName(file), Path.GetFileName(file), archive);

				switch (ext)
				{
					case ".LUA":
						sortedFiles[LoadOrdering.LuaScript].Add(fileInformation);
						break;
					case ".LUASES":
						sortedFiles[LoadOrdering.LuaSession].Add(fileInformation);
						break;
					case ".STATE":
						sortedFiles[LoadOrdering.State].Add(fileInformation);
						break;
					case ".CHT":
						sortedFiles[LoadOrdering.Cheat].Add(fileInformation);
						break;
					case ".WCH":
						sortedFiles[LoadOrdering.Watch].Add(fileInformation);
						break;
					case ".CDL":
						sortedFiles[LoadOrdering.CdFile].Add(fileInformation);
						break;
					default:
						if (MovieService.IsValidMovieExtension(ext))
						{
							sortedFiles[LoadOrdering.MovieFile].Add(fileInformation);
						}
						else if (MovieImport.IsValidMovieExtension(ext))
						{
							sortedFiles[LoadOrdering.LegacyMovieFile].Add(fileInformation);
						}
						else if (KnownRomExtensions.Contains(ext))
						{
							if (string.IsNullOrEmpty(archive) || !_nonArchive.Contains(ext))
							{
								sortedFiles[LoadOrdering.Rom].Add(fileInformation);
							}
						}
						else
						{
							/* Because the existing behaviour for archives is to try loading
							 * ROMs out of them, that is exactly what we are going to continue
							 * to do at present.  Ideally, the archive should be scanned and
							 * relevant files should be extracted, but see the note below for
							 * further details.
							 */
							var archiveHandler = new SharpCompressArchiveHandler();

							if (string.IsNullOrEmpty(archive) && archiveHandler.CheckSignature(file, out _, out _))
							{
								sortedFiles[LoadOrdering.Rom].Add(fileInformation);
							}
							else
							{
								// This is hack is to ensure that unrecognized files are treated like ROMs
								sortedFiles[LoadOrdering.Rom].Add(fileInformation);
							}

							/*
							 * This is where handling archives would go.
							 * Right now, that's going to be a HUGE hassle, because of the problem with
							 * saving things into the archive (no) and with everything requiring filenames
							 * and not streams (also no), so for the purposes of making drag/drop more robust,
							 * I am not building this out just yet.
							 * -- Adam Michaud (Invariel)
							
							int offset = 0;
							bool executable = false;
							var archiveHandler = new SevenZipSharpArchiveHandler();

							// Not going to process nested archives at the moment.
							if (String.IsNullOrEmpty (archive) && archiveHandler.CheckSignature(file, out offset, out executable))
							{
								List<string> fileNames = new List<string>();
								var openedArchive = archiveHandler.Construct (file);

								foreach (BizHawk.Common.HawkFileArchiveItem item in openedArchive.Scan ())
									fileNames.Add(item.Name);

								ProcessFileList(fileNames.ToArray(), ref sortedFiles, file);

								openedArchive.Dispose();
							}
							archiveHandler.Dispose();
							 */
						}
						break;
				}
			}
		}

		private void FormDragDrop_internal(DragEventArgs e)
		{
			/*
			 *  Refactor, moving the loading of particular files into separate functions that can
			 *  then be used by this code, and loading individual files through the file dialogue.
			 *  
			 *  Step 1:
			 *	  Build a dictionary of relevant files from everything that was dragged and dropped.
			 *	  This includes peeking into all relevant archives and using their files.
			 *	  
			 *  Step 2:
			 *	  Perhaps ask the user which of a particular file type they want to use.
			 *		  Example:  rom1.nes, rom2.smc, rom3.cue are drag-dropped, ask the user which they want to use.
			 *		  
			 *  Step 3:
			 *	  Load all of the relevant files, in priority order:
			 *	  1) The ROM
			 *	  2) State
			 *	  3) Watch files
			 *	  4) Code Data Logger (CDL)
			 *	  5) LUA sessions
			 *	  6) LUA scripts
			 *	  7) Cheat files
			 *	  8) Movie Playback Files
			 *	  
			 *  Bonus:
			 *	  Make that order easy to change in the code, heavily suggesting ROM and playback as first and last respectively.
			 */

			var filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);
			Dictionary<LoadOrdering, List<FileInformation>> sortedFiles = new Dictionary<LoadOrdering, List<FileInformation>>();

			// Initialize the dictionary's lists.
			foreach (LoadOrdering value in Enum.GetValues(typeof(LoadOrdering)))
			{
				sortedFiles.Add(value, new List<FileInformation>());
			}

			ProcessFileList(HawkFile.Util_ResolveLinks(filePaths), ref sortedFiles);

			// For each of the different types of item, if there are no items of that type, skip them.
			// If there is exactly one of that type of item, load it.
			// If there is more than one, ask.

			foreach (LoadOrdering value in Enum.GetValues(typeof(LoadOrdering)))
			{
				switch (sortedFiles[value].Count)
				{
					case 0:
						break;
					case 1:
						var fileInformation = sortedFiles[value].First();
						string filename = Path.Combine(new[] { fileInformation.DirectoryName, fileInformation.FileName });

						switch (value)
						{
							case LoadOrdering.Rom:
								LoadRom(filename, fileInformation.ArchiveName);
								break;
							case LoadOrdering.State:
								LoadStateFile(filename, fileInformation.ArchiveName);
								break;
							case LoadOrdering.Watch:
								LoadWatch(filename, fileInformation.ArchiveName);
								break;
							case LoadOrdering.CdFile:
								LoadCdl(filename, fileInformation.ArchiveName);
								break;
							case LoadOrdering.LuaSession:
								LoadLuaSession(filename, fileInformation.ArchiveName);
								break;
							case LoadOrdering.LuaScript:
								LoadLuaFile(filename, fileInformation.ArchiveName);
								break;
							case LoadOrdering.Cheat:
								LoadCheats(filename, fileInformation.ArchiveName);
								break;
							case LoadOrdering.MovieFile:
							case LoadOrdering.LegacyMovieFile:
								// I don't really like this hack, but for now, we only want to load one movie file.
								if (sortedFiles[LoadOrdering.MovieFile].Count + sortedFiles[LoadOrdering.LegacyMovieFile].Count > 1)
									break;

								if (value == LoadOrdering.MovieFile)
									LoadMovie(filename, fileInformation.ArchiveName);
								else
									LoadLegacyMovie(filename, fileInformation.ArchiveName);
								break;
						}
						break;
				}
			}
		}
	}
}
