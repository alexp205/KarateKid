﻿using BizHawk.Client.Common.MovieConversionExtensions;

namespace BizHawk.Client.Common.movie.import
{
	// ReSharper disable once UnusedMember.Global
	[ImportExtension(".bkm")]
	public class BkmImport : MovieImporter
	{
		protected override void RunImport()
		{
			var movie = new BkmMovie
			{
				Filename = SourceFile.FullName
			};

			movie.Load(false);
			Result.Movie = movie.ToBk2();
		}
	}
}
