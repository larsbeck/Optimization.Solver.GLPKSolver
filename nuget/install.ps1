param($installPath, $toolsPath, $package, $project)
$project.Object.References | Where-Object { $_.Name -eq 'Optimization.Framework.Contracts' } | ForEach-Object { $_.Remove() }