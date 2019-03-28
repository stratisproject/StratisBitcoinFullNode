// Register click-to-clipboard event
$(document).on("click", '[role="copy"]', function()
{
    var tempField = document.createElement("textarea");
    tempField.value = $("#" + $(this).attr("data-id")).text();
    $(this).parent().append(tempField);
    tempField.select();
    document.execCommand("copy");
    tempField.remove();
    if($(this).attr("data-message") != null)
    {
        Snackbar.show({text: $(this).attr("data-message"), pos: "bottom-center", showAction: true});
    }
});

$(document).ready(function()
{
    var CacheIsDifferent = false;

    // Run SignalR to accept events from the backend
    NProgress.start();
    var signalrHub = new signalR.HubConnectionBuilder().withUrl("/ws-updater").build();
    signalrHub.on("CacheIsDifferent", function () {
        NProgress.start();
        CacheIsDifferent = true;
        if($('.modal').hasClass('show') == false)
        {
            $("#container").load("/update-dashboard");
            Snackbar.close();
        }
        NProgress.done();
    });
    signalrHub.on("NodeUnavailable", function () {
        $(".status").text("API Unavailable");
        Snackbar.show({text: "The full nodes API are unavailable", pos: "bottom-center", duration: 0, actionText: 'REFRESH', onActionClick: function() {document.location.reload();}});
    });
    signalrHub.start();

    // Detects modal closing for refresh the dashboard
    $('.modal').on('hidden.bs.modal', function () {
        if(CacheIsDifferent)
        {
            $("#container").load("/update-dashboard");
            Snackbar.close();
        }
    });

    // Prevent modal disclosure
    $(".close").click(function()
    {
        $('.modal').modal('hide')
    });

    // Check if the federation is enabled, if it's not the case a modal is displayed to enable it
    $.get("/check-federation", function(response)
    {
        if(response == false)
        {
            $("#enableF").modal("show");
        }
    });

    NProgress.done();

    $(".copy-clipboard").click(function()
    {
        var tempField = document.createElement("textarea");
        tempField.value = $(this).parent().find("code").text();
        $(this).parent().find("code").append(tempField);
        tempField.select();
        document.execCommand("copy");
        tempField.remove();
        Snackbar.show({text: "Mining Public Key Copied to Clipboard !", pos: "bottom-center", showAction: true});
    });
});

function DisplayNotification(text)
{
    setTimeout(function()
    {
        Snackbar.show({text: text, pos: "bottom-center", showAction: true});
    }, 1000);
}

function BeginAction()
{
    NProgress.start();
}
function CompleteAction()
{
    NProgress.done();
}

function HideModals()
{
    $(".modal").modal("hide");
}

function EnabledFederation()
{
    DisplayNotification("The federation is enabled.");
}
function EnableFederationFailed()
{
    DisplayNotification("Unable to enable the federation.");
}

/* STRATIS MAINNET ACTIONS EVENT */
function StratisNodeStopped()
{
    DisplayNotification("Mainnet node sucessfully stopped.");
}
function StratisNodeStopFailed()
{
    DisplayNotification("Mainnet node cannot be stopped.");
}

function StratisCrosschainResynced()
{
    DisplayNotification("Mainnet crosschain Sucessfully resynced.");
}
function StratisCrosschainResyncFailed()
{
    DisplayNotification("Unable to resynced Mainnet crosschain .");
}

function StratisResyncedBlockchain()
{
    DisplayNotification("Mainnet blockchain sucessfully resynced.");
}
function StratisResyncBlockchainFailed()
{
    DisplayNotification("Unable to resync the blockchain.");
}

/* SIDECHAIN ACTIONS EVENT */
function SidechainNodeStopped()
{
    DisplayNotification("Mainnet node sucessfully stopped.");
}
function SidechainNodeStopFailed()
{
    DisplayNotification("Mainnet node cannot be stopped.");
}

function SidechainCrosschainResynced()
{
    DisplayNotification("Mainnet crosschain Sucessfully resynced.");
}
function SidechainCrosschainResyncFailed()
{
    DisplayNotification("Unable to resynced Mainnet crosschain .");
}

function SidechainResyncedBlockchain()
{
    DisplayNotification("Mainnet blockchain sucessfully resynced.");
}
function SidechainResyncBlockchainFailed()
{
    DisplayNotification("Unable to resync the blockchain.");
}