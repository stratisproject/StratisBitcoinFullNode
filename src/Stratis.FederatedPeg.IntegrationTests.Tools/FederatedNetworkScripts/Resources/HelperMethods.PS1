[System.Collections.ArrayList]$node_configurations = @()

# Method to start a node using specified parameters
function Start-Node {
    param( [string]$Path, [string]$WindowTitle, [string]$ConsoleColor, [string]$CmdArgs, [int]$Timeout )

    cd $Path
    start-process cmd -ArgumentList "/k title ${WindowTitle} && color ${ConsoleColor} && dotnet run ${CmdArgs}"
    $node_configurations.Add(@{
        id = "node" + $node_configurations.Count
        title= $WindowTitle
        port = Select-String -InputObject $CmdArgs -Pattern "-apiport=(\d+)" | % {$_.Matches.Groups[1].Value}
        args = $CmdArgs
    })
    timeout $Timeout
}

# Method that uses the node setup collected by Start-Node to create a debugging dashboard for started nodes
# Requires the variables $browser and $dashboard_path defined on the main ps script
function Create-Dashboard {
    Write-Output "Creating dashboard..."

    $html = @"
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no">

<title>Debug Dashboard</title>

<link rel="stylesheet" href="https://stackpath.bootstrapcdn.com/bootstrap/4.1.3/css/bootstrap.min.css" integrity="sha384-MCw98/SFnGE8fJT3GXwEOngsV7Zt27NXFoaoApmYm81iuXoPkFOJwJ8ERdknLPMO" crossorigin="anonymous">

<script src="https://code.jquery.com/jquery-3.3.1.slim.min.js" integrity="sha384-q8i/X+965DzO0rT7abK41JStQIAqVgRVzpbzo5smXKp4YfRvH+8abtTE1Pi6jizo" crossorigin="anonymous"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.14.3/umd/popper.min.js" integrity="sha384-ZMP7rVo3mIykV+2+9J3UJ46jBk0WLaUAdn689aCwoqbBJiSnjAK/l8WvCWPIPm49" crossorigin="anonymous"></script>
<script src="https://stackpath.bootstrapcdn.com/bootstrap/4.1.3/js/bootstrap.min.js" integrity="sha384-ChfqqxuZUCnJSK3+MXmPNIyE6ZbWh2IMqE241rYiqJxyMiZ6OW/JmZQ5stwEULTy" crossorigin="anonymous"></script>

<style>
	iframe{
		overflow:scroll;
		position:absolute;
		top:40px;
		left:0px;
		right:0px;
		bottom:0px
	}
</style>

</head>
 <body>
    <ul class="nav nav-tabs" id="myTab" role="tablist">
        <li class="nav-item">
            <a class="nav-link active" id="summary-tab" data-toggle="tab" href="#summary" role="tab">Summary</a>
        </li>
"@

    #create navigators
    foreach ($node in $node_configurations) {
        $html = $html + @"
        <li class="nav-item">
            <a class="nav-link" id="$($node.id)-tab" data-toggle="tab" href="#$($node.id)" role="tab">$($node.title)</a>
        </li>
"@
    }


    #create summary text
    $summaryText = ""
    foreach ($node in $node_configurations) {
        $summaryText = $summaryText + @"
        <hr>
        <pre>
        Node: $($node.title)
        Arguments: $($node.args)
        </pre>
"@
    }

    #create tabs
    $html = $html + @"
    </ul>
    <div class="tab-content" id="myTabContent">
        <div class="tab-pane fade show active" id="summary" role="tabpanel">
            $summaryText
        </div>
"@

    foreach ($node in $node_configurations) {
        $html = $html + @"
        <div class="tab-pane fade" id="$($node.id)" role="tabpanel">
	        <iframe frameBorder="0", width="100%", height="100%" id="frame-$($node.id)" src="http://localhost:$($node.port)/"></iframe>
        </div>
"@
    }

    $html = $html + @"
    </div>
<script>
`$(function(){
	function refreshTab(tab){
		var id = tab.id.replace("-tab","");
		`$("#frame-" + id).each(function() {
			var src= `$(this).attr('src');
			`$(this).attr('src',src);
		});
	}

	var selectedTab = null;
	window.setInterval(function(){
		if(selectedTab!=null){
			refreshTab(selectedTab);
		}
	}, 5000);

	`$('#myTab a').on('click', function (e) {
		selectedTab = e.target;
		refreshTab(selectedTab);
	})
});
</script>
</body>
</html>
"@

    Out-File $dashboard_path -InputObject $html
    Write-Output "Dashboard created at $dashboard_path"
    Start-Process -FilePath $browser -ArgumentList "$dashboard_path"
}