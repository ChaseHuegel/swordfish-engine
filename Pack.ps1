$packagesPath = '.packages'

$projects = @(
    'Swordfish',
    'Swordfish.Integrations',
    'Swordfish.Library'
)

foreach ($project in $projects) {
    $path = '{0}/{0}.csproj' -f $project
    Write-Host $path
    dotnet pack $path -c Release -o $packagesPath
}