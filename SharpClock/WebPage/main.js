$(function () {
    console.log("ready!");
    const languageDictionary = {
        pl:
        {
            languageName: "pl",
            on: "On",
            off: "Off",
            module: "Moduł",
            moduleSetting: "Ustawienia Modułów",
            options: "Opcje"
        },
        en:
        {
            languageName: "en",
            on: "On",
            off: "Off",
            module: "Module",
            moduleSetting: "Module Setting",
            options: "Options"
        }
    };
    let lang = languageDictionary.en;
    if (navigator.language == "pl-PL")
        lang = languageDictionary.pl;
    //$("#Loader").hide();
    LoadProperties();
    LoadModuleList();
    LoadAppInstall();
    LoadDllList();
    M.AutoInit();
    //Sortowanie
    $("#moduleList").sortable({
        axis: "y",
        containment: $("#moduleList").parent(),
        cursor: "grabbing",
        delay: 150,
        stop: function (event, ui) {
            $("#Loader").show();
            const sort = [];
            $("#moduleList li").each(function () {
                sort.push($(this).find("form").serializeArray()[0].value);
            });
            //const current = ui.item.find("form").serializeArray()[0].value;
            //const index = sort.indexOf(current);
            $.post("sortModules", { order: sort }, function (result) {
                $("#Loader").hide();
            });
            console.log(sort);
        }
    });
    $("#moduleList").disableSelection();
    //Load Module List
    function LoadModuleList() {
        $("#Loader").show();
        $.post("getModules", function (result) {
            $("#moduleList").html("");
            console.log(result.Response);
            result.Response.forEach(function (prop) {
                //Generowanie stringa//
                let values = `<form class="noActionForm" method="POST" action="">`;
                values += `<input type="hidden" name="Module" value="${prop.Name}"/>`;

                //Start Stop Module
                values += `<p><label>${lang.module}</label>`;
                values += `<div class="switch"><label for="${prop.Name}_Power">`;
                values += `${lang.off}`;
                values += `<input type="checkbox" id="${prop.Name}_Power" name="Power" ${prop.Status == "started" ? "checked" : ""} />`;
                values += `<span class="lever"></span>`;
                values += `${lang.on}`;
                values += `</label></div></p><br />`;

                prop.Values.forEach(function (Value) {
                    let tmp = `<p>`;
                    let visibleName;
                    try {
                        visibleName = Value.VisibleName.find((value) => value.lang == lang.languageName).value;
                    } catch (e) {
                        try {
                            visibleName = Value.VisibleName.find((value) => value.lang == "en").value;
                        } catch (e) {
                            visibleName = Value.Name;
                        }
                    }
                    
                    //Number
                    if (Value.type == "Int32" || Value.type == "float") {
                        tmp += `<label for="${prop.Name}_${Value.Name}">`;
                        tmp += `<span>${visibleName}</span>`;
                        tmp += `<input type="number" id="${prop.Name}_${Value.Name}" name="${Value.Name}" value="${Value.Value}" />`;
                        tmp += `</label>`;
                    }
                    //Checkbox
                    else if (Value.type == "Boolean") {
                        tmp += `<label>${visibleName}</label>`;
                        tmp += `<div class="switch"><label for="${prop.Name}_${Value.Name}">`;
                        tmp += `Off`;
                        tmp += `<input type="checkbox" id="${prop.Name}_${Value.Name}" name="${Value.Name}" ${Value.Value == "True" ? "checked" : ""} />`;
                        tmp += `<span class="lever"></span>`;
                        tmp += `On`;
                        tmp += `</label></div>`;
                    }
                    //String
                    else if (Value.type == "String") {
                        tmp += `<label for="${prop.Name}_${Value.Name}">`;
                        tmp += `<span>${visibleName}</span>`;
                        tmp += `<input type="text" id="${prop.Name}_${Value.Name}" name="${Value.Name}" value="${Value.Value}" />`;
                        tmp += `</label>`;
                    }
                    //RadioButton
                    else if (Value.type == "Enum") {
                        let radioOptions = Value.options.split(",");
                        tmp += `<label>${visibleName}</label><br/>`;
                        radioOptions.forEach(function (radioOption) {
                            let visibleRadioName;
                            try {
                                visibleRadioName = Value.visibleOptions.find((value) => value.lang == lang.languageName).values;
                                visibleRadioName = visibleRadioName.find((value) => value.split("=")[0] == radioOption).split("=")[1];

                            } catch (e) {
                                try {
                                    visibleRadioName = Value.visibleOptions.find((value) => value.lang == "en").value;
                                    visibleRadioName = visibleRadioName.find((value) => value.split("=")[0] == radioOption).split("=")[1];
                                } catch (e) {
                                    visibleRadioName = radioOption;
                                }
                            }

                            tmp += `<label for="${prop.Name}_${Value.Name}_${radioOption}">`;
                            tmp += `<input type="radio" id="${prop.Name}_${Value.Name}_${radioOption}" name="${Value.Name}" value="${radioOption}" ${Value.Value == radioOption ? `checked` : ""} />`;
                            tmp += `<span>${visibleRadioName}</span>`;
                            tmp += `</label>`;
                            tmp += `<br />`;
                        });
                    }
                    //Colorpicker
                    else if (Value.type == "Color") {
                        tmp += `<label>${visibleName}</label><br/>`;
                        tmp += `<label for="${prop.Name}_${Value.Name}">`;
                        tmp += `<input type="color" id="${prop.Name}_${Value.Name}" name="${Value.Name}" value="${Value.Value}"/>`;
                        tmp += `</label>`;
                        tmp += `<br />`;
                    }
                    //TimeSpan
                    else if (Value.type == "TimeSpan") {
                        tmp += `<label>${visibleName}</label><br/>`;
                        tmp += `<label for="${prop.Name}_${Value.Name}">`;
                        tmp += `<input type="time" id="${prop.Name}_${Value.Name}" name="${Value.Name}" value="${Value.Value}"/>`;
                        tmp += `</label>`;
                        tmp += `<br />`;
                    }
                    //TODO Other...
                    tmp += "</p><br />";
                    values += tmp;
                });
                values += `<div class="right-align">`
                values += `<button type="button" class="waves-effect waves-light btn yellow darken-3" id="${prop.Name}_reloadBtn"><span class="material-icons right">refresh</span></button>`;
                values += `<button type="button" class="waves-effect waves-light btn green darken-3" id="${prop.Name}_switchtoBtn"><span class="material-icons right">visibility</span></button>`;
                values += `</div>`
                values += `</form>`;
                //FormSending
                const string = `
                    <li>
                        <div class="collapsible-header">
                            <i class="material-icons">${prop.Icon}</i>
                            ${prop.Name}
                            <span id="${prop.Name}_isRunning" class="badge ${prop.Status == "started" ? "green" : "red"}-text"><span style="font-weight:bold" class="material-icons">power_settings_new</span></span>
                        </div>
                        <div class="collapsible-body white">
                            ${values}
                        </div>
                    </li>`
                $("#moduleList").append(string);
                $(`#${prop.Name}_reloadBtn`).click(function () {
                    $("#Loader").show();
                    $.post("reloadModule", { name: prop.Name }, function (result) {
                        $("#Loader").hide();
                    });
                })
                $(`#${prop.Name}_switchtoBtn`).click(function () {
                    $("#Loader").show();
                    $.post("switchModule", { name: prop.Name, pause: true }, function (result) {
                        if (result.Response.Error)
                            alert("Module is disabled!");
                        pauseButton(result.Response.Pause);
                        $("#Loader").hide();
                    },"JSON");
                })
            });
            $('.fixed-action-btn').floatingActionButton({ direction: "left" });
            $(".noActionForm").submit(function (event) {
                event.preventDefault();
                $("#Loader").show();
                const form = SerializeForm(this);
                //console.log(form);
                $.post("modifyModuleSettings", form, function (result) {
                    //console.log(result);
                    if (result.Response.Status)
                        $('#' + result.Response.Name + "_isRunning").removeClass("red-text").addClass("green-text");
                    else
                        $('#' + result.Response.Name + "_isRunning").removeClass("green-text").addClass("red-text");

                    $("#Loader").hide();
                }, "JSON");
            });
            $(".noActionForm :input").change(function (event) {
                //console.log(event.target);
                $(this).submit();
            });
            $("#Loader").hide();

        }, "JSON");
    }
    function LoadAppInstall() {
        $("#AppInstall_Save").click(function () {
            $("#Loader").show();
            const files = $("#AppInstall_File")[0].files;
            console.log(files);
            if (files.length > 0) {
                const form = new FormData();
                form.append("file", files[0], files[0].name);
                $.ajax({
                    type: "POST",
                    contentType: false,
                    cache: false,
                    processData: false,
                    url: "AppInstall",
                    dataType: 'json',
                    data: form
                }).done(response => {
                    console.log(response);
                }).always(() => {
                    $("#Loader").hide();
                    alert("Upload Complete");
                });
            }
            else {
                $("#Loader").hide();
            }
        });
    }
    function LoadDllList() {
        $("#Loader").show();
        $.post("GetDlls", function (result) {
            $("#InstaledDll").html("");
            result.Response.forEach((m) => {
                //TODO delete dll
                $("#InstaledDll").append(`<li class="collection-item"><div>${m}<a href="#!" class="secondary-content"><i class="material-icons">delete</i></a></div></li>`);
            });
            $("#Loader").hide();
        }, "JSON");
    }
    function LoadProperties() {
        $("#brightnessControl").mousemove(function () {
            const val = $(this).val();
            $("#brightnessValue").html(val + "%");
        });
        $.post("getProperties", function (result) {
            //console.log(result);
            const val = mapValue(result.Response.Brightness, 2, 32, 0, 100);
            $("#brightnessControl").val(val);
            $("#brightnessValue").html(val+"%");
            $("#brightnessControl").change(function () {
                $("#Loader").show();
                const val = mapValue($(this).val(), 0, 100, 2, 32);
                $.post("Brightness", { Value: val }, function (result) {
                    $("#Loader").hide();
                }, "JSON");
            });
            pauseButton(result.Response.Pause);
            //TODO animated switch
        },"JSON");
    }
    
    $("#nextModuleBtn").click(function () {
        $.post("nextModule", function (result) {
        }, "JSON");
    });
    $("#pauseBtn").click(function () {
        $.post("pause", function (result) {
            pauseButton(result.Response.Pause);
        }, "JSON");
    });
    $("#stopBtn").click(function () {
        $.post("shutdown", { hard: "false" }, function (result) {
        }, "JSON");
    });
    $("#shutdownBtn").click(function () {
        $.post("shutdown", { hard: "true" }, function (result) {
        }, "JSON");
    });
    $("#reloadBtn").click(function () {
        $.post("shutdown", { hard: "restart" }, function (result) {
        }, "JSON");
    });
    
});
function pauseButton(value) {
    $("#pauseBtn").html(value == true ? "Module auto switch<i class='material-icons right'>play_arrow</i>" : "Module auto switch <i class='material-icons right'>pause</i>");
    if (value == true) {
        $("#pauseBtn").removeClass("green");
        $("#pauseBtn").addClass("yellow darken-3");
    }
    else {
        $("#pauseBtn").removeClass("yellow darken-3");
        $("#pauseBtn").addClass("green");
    }
}
function SerializeForm(formName) {
    let str = $(formName).find('input:not([type="checkbox"])').serialize();
    let str1 = $(formName).find('input[type="checkbox"]').map(function () { return this.name + "=" + this.checked; }).get().join("&");
    if (str1 != "" && str != "") str += "&" + str1;
    else str += str1;
    str = str.split("&");
    const form = {};
    str.forEach(function (prop) {
        prop = prop.split("=");
        form[prop[0]] = decodeURIComponent(prop[1]);
    });
    return form
}
function mapValue(input, input_start, input_end, output_start, output_end) {
    return Math.round((input - input_start) * output_end / input_end + output_start);
}