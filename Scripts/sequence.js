
jQuery(document).ready(function ($) {

    
});

function sequence() {
}

sequence.prototype.submitAction = function (a) {

    var action = "";

    switch (a) {
        case 'view': action = "GetRuns"; break;
        case 'details': action = "Details"; break;
        case 'delete': action = "Delete"; break;
    }

    log(action);

    $('#secondForm').attr('action', 'http://localhost/GCOnline/GCData/' + action);

    $('#secondForm').submit();
}


var sequence = new sequence();