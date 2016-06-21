
jQuery(document).ready(function ($) {

});

function common() {

    this.retTime = 0;
    this.conc = 0;
    this.oldconc = this.conc;
    this.runID = 0;

    this.standardsArray = {
        C10_FFA: { array: [] },
        C12_FFA: { array: [] },
        C14_FFA: { array: [] },
        C16_FFA: { array: [] },
        C18_FFA: { array: [] },
        C12_MAG: { array: [] },
        C20_FFA: { array: [] },
        C14_MAG: { array: [] },
        C16_MAG: { array: [] },
        C18_MAG: { array: [] },
        C12_DAG: { array: [] },
        C14_DAG: { array: [] },
        C11_TAG: { array: [] },
        C16_DAG: { array: [] },
        C12_TAG: { array: [] },
        C18_DAG: { array: [] },
        C14_TAG: { array: [] },
        C16_TAG: { array: [] },
        C17_TAG: { array: [] },
        C18_TAG: { array: [] },
        C20_TAG: { array: [] },
        C22_TAG: { array: [] }
    };
}

common.prototype.selectAll = function (el) {

    var parent = $(el).parents('table');
    $(parent).find('td input:checkbox').each(function () {
        if ($(el).is(":checked")) {
            $(this).attr('checked', true);
        } else {
            $(this).attr('checked', false);                
        }
    });
    
}

common.prototype.drawStandardsPopup = function () {

    var html = "<table><tr><th colspan='2'>Standards</th></tr>";

    var count = 0;
    for (var i in common.standardsArray) {

        var time = "";

        for (var j in common.currentCali) {
            if (common.currentCali[j].Compound == i) { time = common.currentCali[j].Time; break; }
        }

        if (count % 2 == 0) {
            html = html + "<tr><td><a href='javascript:void(0);' onclick='common.setStandard(this, \"" + i + "\")'>" + i + "</a><b> " + time + "</b></td>";
        } else {
            html = html + "<td><a href='javascript:void(0);' onclick='common.setStandard(this, \"" + i + "\")'>" + i + "</a><b> " + time + "</b></td></tr>";
        }
        count++;
    }

    html = html + "</table>";
    $(".standards_container").html(html);

}

common.prototype.showNewQuantTable = function () {
    $(".new_quant_range").show();
}

common.prototype.createNewQuantificationRange = function () {

    var rangeName = $('#new_quant_range_name').val();

    var range = { RangeName: rangeName };

    var quantList = new Array();

    $(".new_quant_range table tr").each(function () {

        var compound = $(this).find('.rt_start').attr('id');
        var rt_start = $(this).find('.rt_start').val();
        var rt_end = $(this).find('.rt_end').val();

        if (rt_start) {
            switch (compound) {

                case "FFA": range.FFA_Start = rt_start; range.FFA_End = rt_end; break;
                case "MAG": range.MAG_Start = rt_start; range.MAG_End = rt_end; break;
                case "DAG": range.DAG_Start = rt_start; range.DAG_End = rt_end; break;
                case "TAG": range.TAG_Start = rt_start; range.TAG_End = rt_end; break;
                default:
                    quantList.push({ RangeName: rangeName, Compound: compound, RT_Start: rt_start, RT_End: rt_end });
                    break;
            }
        }
    });

    var wrapper = { range: range, quantList: quantList };

    $.ajax({
        type: 'POST',
        url: 'http://localhost/GCOnline/Quantification/SaveQuantification',
        contentType: 'application/json; charset=utf-8',
        data: $.toJSON(wrapper),
        success: function (data, status) {
            common.insertNewQuantRange(data);
        }
    });

}

common.prototype.quantify = function () {

    var rangeID = "";
    var caliName = "";

    $(".select_cali").each(function () {

        if ($(this).attr('checked') == true) { caliName = $(this).attr('id'); }

    });

    $(".select_range").each(function () {

        if ($(this).attr('checked') == true) { rangeID = $(this).attr('id'); }

    });

    var runList = new Array();

    $(".run_checkbox:checked").each(function () {

        var id = $(this).attr('id');
        var dilution = $('#' + id + '_dilution').val();
        var biomass = $('#' + id + '_biomass').val();
        var sample_volume = $('#' + id + '_sample_volume').val();
        var biomass_volume = $('#' + id + '_biomass_volume').val();
        var biomass_cmmol = $('#' + id + '_biomass_cmmol').val();
        var cell_count = $('#' + id + '_cell_count').val();
        var group = $('#' + id + '_group').val();
        var runName = $('#' + id + '_dilution').attr('name');
        var tempArray = { RangeID: rangeID, RunID: id, RunName: runName, CalibrationName: caliName, DilutionFactor: dilution, BMWeight: biomass, SampleVolume: sample_volume, BMVolume: biomass_volume, BMWeightCmmol: biomass_cmmol, CellCount: cell_count, Group: group };

        runList.push(tempArray);
    });

    var wrapper = { runList: runList };

    $.ajax({
        type: 'POST',
        url: 'http://localhost/GCOnline/Quantification/Quantify',
        contentType: 'application/json; charset=utf-8',
        data: $.toJSON(wrapper),
        success: function (data, status) {
            common.showQuantification(data);
        }
    });

}

common.prototype.showQuantification = function (d) {

    var data = jQuery.parseJSON(d);

    log(data);

    var html = "<table><tr><th>Run Name</th><th>Total FFA</th><th>Total MAG</th><th>Total DAG</th><th>Total TAG</th><th>Total Lipid</th></tr>";

    for (var i in data) {
        var totalLipid = data[i].TotalFFA + data[i].TotalMAG + data[i].TotalDAG + data[i].TotalTAG;
        html = html + "<tr><td>" + data[i].RunName + "</td><td>" + data[i].TotalFFA + "</td><td>" + data[i].TotalMAG + "</td><td>" + data[i].TotalDAG + "</td><td>" + data[i].TotalTAG + "</td><td>" + totalLipid + "</td></tr>";
    }

    html = html + "</table>";
    $("#main").html(html);

}

common.prototype.insertNewQuantRange = function (d) {

    var newRange = jQuery.parseJSON(d);

    $('#range_list:checked').each(function () { $(this).attr('checked', false); });
    $('#range_list').append("<tr><td><input type='checkbox' id='" + newRange.RangeID + "' checked='checked'/></td><td>" + newRange.RangeName + "</td></tr>");
}

common.prototype.generatePeakCharts = function (d) {

    var raw = jQuery.parseJSON(d);

    var data = raw.Value;

    this.currentCali = raw.Key;

    this.chartArray = new Array();

    for (var i in data) {

        var peakData = data[i];
        var peakArray = new Array();
        var runId = 0;
        //var conc = i.split('_')[1];

        for (var j in peakData) {
            var o = [peakData[j].Time, peakData[j].Height, i];
            peakArray.push(o);
            runId = peakData[j].RunID;
        }

        $('#main').append('<div class="chart_div" id="chart_cont_' + i + '"></div><div class="slider" id="slider_' + i + '" conc="' + i + '"></div>');

        var max = 500;
        switch (i) {
            case '0.005': max = 5; break;
            case '0.01': max = 100; break;
            case '0.5': max = 700; break;
            default: break;
        }

        var chart = new Highcharts.Chart({
            chart: {
                renderTo: 'chart_cont_' + i,
                type: 'column',
                width: 1080,
                borderRadius: 5
            },
            legend: {
                enabled: false
            },
            title: {
                text: 'Chromatogram: ' + i + '(mg/mL)'
            },
            xAxis: {
                title: { text: 'Retention Time' }
            },
            yAxis: {
                title: { text: 'Peak Height' },
                max: max
            },
            plotOptions: {
                series: {
                    lineWidth: 5,
                    shadow: false,
                    color: '#8855ff',
                    borderWidth: 2,
                    borderColor: 'black',
                    borderRadius: 2,
                    point: {
                        events: {
                            click: function () {
                                common.columnWasClicked(this);
                            }
                        }
                    }
                }
            },
            series: [{ name: "Peak Height", data: peakArray}]
        });

        this.chartArray[i] = { Conc: i, Chart: chart, RunID: runId };

    }

    common.addSliders();
}

common.prototype.columnWasClicked = function (e) {

    var mouse_x = e.pageX + 20;
    var mouse_y = e.pageY - 150;

    $(".standards_container").css('top', mouse_y + 'px');
    $(".standards_container").css('left', mouse_x + 'px');
    $(".standards_container").show();

    this.retTime = e.x;
    this.conc = e.config[2];
    this.runID = this.chartArray[this.conc].RunID;

    if (this.conc != this.oldconc) {
        this.drawStandardsPopup();
    }
    this.oldconc = this.conc;
}

common.prototype.setStandard = function (el, standard) {

    this.standardsArray[standard].array.push({ Time: this.retTime, Conc: this.conc, RunID: this.runID });
    $(".standards_container").hide();
    $(el).hide();

    this.drawCaliTable();
}

common.prototype.removeStandard = function (conc, time, runId) {

    for (var i in this.standardsArray) {
        var array = this.standardsArray[i].array;
        for (var j = 0; j < array.length; j++) {
            var obj = array[j];
            if (obj.Time == time && obj.Conc == conc && obj.RunID == runId) {
                array.pop(j);
                break;
            }
        }
    }

    this.drawCaliTable();
    this.drawStandardsPopup();

    log(this.standardsArray);
}

common.prototype.drawCaliTable = function () {

    var html = "";

    for (var i in this.standardsArray) {
        html = html + "<div class='std_col'>" + i;
        var r = this.standardsArray[i].array;
        for (var j in r) {
            html = html + "<div class='std_row'>";
            html = html + "<a href='javascript:void(0);' onclick='common.removeStandard(" + r[j].Conc + "," + r[j].Time + "," + r[j].RunID + ")'>" + r[j].Conc + "(mg/ml) <br/> " + r[j].Time + " min</a>";
            html = html + "</div>";
        }

        html = html + "</div>";
    }

    html = html + "<div style='clear:both;'><a href='javascript:void(0);' class='.uibutton' onclick='common.generateCaliCurve()'>Generate Cali Curve</a></div>";

    $('.cali_container').html(html);
    $('.cali_container').show();
    $('.conc_container').html('');
    $('.std_selector').html('');
}

common.prototype.addSliders = function () {

    $(".slider").slider({
        change: function (event, ui) {
            var conc = $(this).attr('conc');
            var chart = common.chartArray[conc].Chart;
            chart.yAxis[0].setExtremes(0, (ui.value * 10));
        }
    });
}

common.prototype.addtocali = function () {

    var html = "<table><tr><th>Run Name</th><th>Concentration (mg/mL)</th></tr>";

    $(".cali_check:checked").each(function () {
        var conc = $(this).val().split(" ", 1);
        html = html + "<tr><td>" + $(this).val() + "</td><td><input type='text' class='cali_conc' id='" + $(this).attr("id") + "' value='" + conc +"'/></td></tr>";
    });

    html = html + "<tr><td></td><td><a href='javascript:void(0);' onclick='common.getCaliPeaks()'>Get Peaks</a></td></tr></table>";
    $(".conc_container").html(html).show();

}

common.prototype.showCalibrationResuts = function (d) {

    this.calibration = jQuery.parseJSON(d);


    var html = "<div class='std_col'>STATS"
        html = html + "<div class='std_row t_left'>Slope</div>";
        html = html + "<div class='std_row t_left'>Intercept</div>";
        html = html + "<div class='std_row t_left'>SlopePM</div>";
        html = html + "<div class='std_row t_left'>InterceptPM</div>";
        html = html + "<div class='std_row t_left'>RSQ</div>";
        html = html + "<div class='std_row t_left'>STEYX</div>";
        html = html + "<div class='std_row t_left'>FStat</div>";
        html = html + "<div class='std_row t_left'>DegreeFreedom</div>";
        html = html + "<div class='std_row t_left'>RegSumSquares</div>";
        html = html + "<div class='std_row t_left'>ResidualSS</div>";
    html = html + "</div>";

    for (var i in this.calibration) {
        html = html + "<div class='std_col'>" + this.calibration[i].Compound;
            html = html + "<div class='std_row t_right bold'>" + this.roundNumber(this.calibration[i].Slope, 2) + "</div>";
            html = html + "<div class='std_row t_right'>" + this.roundNumber(this.calibration[i].Intercept, 2) + "</div>";
            html = html + "<div class='std_row t_right'>" + this.roundNumber(this.calibration[i].SlopePM, 2) + "</div>";
            html = html + "<div class='std_row t_right'>" + this.roundNumber(this.calibration[i].InterceptPM, 2) + "</div>";
            html = html + "<div class='std_row t_right bold'>" + this.roundNumber(this.calibration[i].RSQ, 2) + "</div>";
            html = html + "<div class='std_row t_right'>" + this.roundNumber(this.calibration[i].STEYX, 2) + "</div>";
            html = html + "<div class='std_row t_right'>" + this.roundNumber(this.calibration[i].FStat, 2) + "</div>";
            html = html + "<div class='std_row t_right'>" + this.roundNumber(this.calibration[i].DegreeFreedom, 2) + "</div>";
            html = html + "<div class='std_row t_right'>" + this.roundNumber(this.calibration[i].RegSumSquares, 2) + "</div>";
            html = html + "<div class='std_row t_right'>" + this.roundNumber(this.calibration[i].ResidualSS, 2) + "</div>";
        html = html + "</div>";
    }

    html = html + "<div class='clear' style='padding-top:10px;'><input type='checkbox' id='current_cali' checked='checked'/><span style='margin:0 5px;'>Default?</span><input type='text' onfocus='this.value=\"\";' id='caliName' value='Calibration Name' style='width:150px;margin:0 5px 5px 0;'/><a href='javascript:void(0);' onclick='common.saveCalibration()'>Save Calibration</a></div>";


    $('.cali_container').html(html);
    $('.chart_div').each(function () { $(this).hide(); });
    $('.slider').each(function () { $(this).hide(); });
}

common.prototype.roundNumber = function (num, dec) {
    var result = Math.round(num * Math.pow(10, dec)) / Math.pow(10, dec);
    return result;
}

common.prototype.submitQuantification = function () {

    $('#quantify_form').submit();

}

common.prototype.saveCalibration = function () {

    var caliName = $('#caliName').val();

    for (var i in this.calibration) {
        this.calibration[i].CalibrationName = caliName;
        if ($('#current_cali').is(":checked")) {
            this.calibration[i].Current = 1;
        }
    }

    var wrapper = { cali: this.calibration };

    $.ajax({
        type: 'POST',
        url: 'http://localhost/GCOnline/Calibration/SaveCaliCurve',
        contentType: 'application/json; charset=utf-8',
        data: $.toJSON(wrapper),
        success: function (data, status) {
            window.location.href = "http://localhost/GCOnline/Calibration";
        }
    });
}

common.prototype.generateCaliCurve = function () {

    var data = new Array();

    for (var i in this.standardsArray) {

        var a = this.standardsArray[i].array;
        
        for (var j in a) {
            
            var tempArray = { Compound: i, RunID: a[j].RunID, Time: a[j].Time, Conc: a[j].Conc };
            
            data.push(tempArray);
        }
    }

    var wrapper = { caliCurve: data };

    $.ajax({
        type: 'POST',
        url: 'http://localhost/GCOnline/Calibration/GetCaliCurve',
        contentType: 'application/json; charset=utf-8',
        data: $.toJSON(wrapper),
        success: function (data, status) {
            common.showCalibrationResuts(data);
        }
    });
}

common.prototype.getCaliPeaks = function () {

    var data = new Array();

    $(".cali_conc").each(function () {
        data.push({ RunID: $(this).attr('id'), Conc: $(this).val() });
    });

    var wrapper = { cali: data };

    $.ajax({
        type: 'POST',
        url: 'http://localhost/GCOnline/Calibration/GetCaliPeaks',
        contentType: 'application/json; charset=utf-8',
        data: $.toJSON(wrapper),
        success: function (data, status) {
            common.generatePeakCharts(data);
        }
    });
}

var common = new common();

function log(obj) {
    console.log(obj);
}
