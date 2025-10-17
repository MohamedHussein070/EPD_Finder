$(function () {
    initFormSubmit();
    initCopyButtons();
    initFileInput();
    initClearButtons();
    initInputSwitch();
    initTextInput();
});

// Global variables
var totalLinks = 0;
var foundLinks = 0;
var failedLinks = 0;
var currentSSE = null;

function initClearButtons() {
    $("#clearTextBtn").click(function () {
        $("#enumbersText").val('');
    });

    $("#clearFileBtn").click(function () {
        $("#fileInput").val('');
        $("#fileName").text('Ingen fil vald').removeClass("text-warning");
        $("#fileCheck").hide();
    });
}

function reset() {
    if (currentSSE) {
        currentSSE.close();
        currentSSE = null;
    }

    // Reset counters
    totalLinks = 0;
    foundLinks = 0;
    failedLinks = 0;

    // Clear previous results
    $("#results").empty();
    $("#resultsInput").val("[]");
    $("#linksCounter").html("");
    $("#downloadForm").hide();
    $("#progressBar").css("width", "0%");
    $("#progressBarText").text("0 %");
    $("#progressText").html("");
    $("#loadingContainer").hide();
    $("#outputTable").hide();
    window.scrollTo({
        top: 0,
        behavior: 'instant'
    });
}

// Switch input type
function initInputSwitch() {
    var viewHistory = [];

    function switchView(newView) {
        var currentView = $(".input-section:visible").attr("id");

        // if user clicks settings button again → go back
        if (currentView === newView && newView === "SettingsDiv" && viewHistory.length > 0) {
            var prev = viewHistory.pop();
            return switchView(prev);
        }

        if (currentView && currentView !== newView) {
            viewHistory.push(currentView);
        }

        // Hide all input sections
        $(".input-section").hide();
        $("#SearchBtn").show();
        $("#BackBtn").hide();
        $("#SubmitError").css("visibility", "hidden");


        // Show new view
        $("#" + newView).show();

        // Update toggle button styles
        $("#toggleTextBtn, #toggleFileBtn, #toggleSettingsBtn").removeClass("btn-primary").addClass("btn-outline-light");
        if (newView === "textInputDiv") {
            $("#toggleTextBtn").addClass("btn-primary").removeClass("btn-outline-light")
        };
        if (newView === "fileInputDiv") {
            $("#toggleFileBtn").addClass("btn-primary").removeClass("btn-outline-light")
        };
        if (newView === "SettingsDiv") {
            $("#toggleSettingsBtn").addClass("btn-primary").removeClass("btn-outline-light")
            $("#SearchBtn").hide();
            $("#BackBtn").show();
        };
    }

    $("#toggleTextBtn").click(() => switchView("textInputDiv"));
    $("#toggleFileBtn").click(() => switchView("fileInputDiv"));
    $("#toggleSettingsBtn").click(() => switchView("SettingsDiv"));

    $("#BackBtn").click(function () {
        if (viewHistory.length > 0) {
            var prev = viewHistory.pop();
            switchView(prev);
        }
    });
}

function initTextInput() {
    $("#enumbersText").on("input", function () {
        if ($(this).val().trim().length > 0) {
            $("#SubmitError").css("visibility", "hidden");
        }
    });
}
// Form submission
function initFormSubmit() {
    $("#epdForm").submit(function (e) {
        e.preventDefault();

        reset();

        var formData = new FormData();

        // Skicka bara det aktiva fältet
        if ($("#textInputDiv").is(":visible")) {
            var manualVal = $("#enumbersText").val().trim();
            if (!manualVal && fileInput) {
                formData.append("file", fileInput);
            }
            if (!manualVal) {
                $("#SubmitError").text("Vänligen ange ett eller flera E-nummer.").css("visibility", "visible");
                return;
            }
            formData.append("eNumbers", manualVal);
        } else if ($("#fileInputDiv").is(":visible")) {
            var fileInput = $("#fileInput")[0].files[0];
            if (!fileInput) {
                $("#SubmitError").text("Vänligen ladda upp en Excel-fil.").css("visibility", "visible");
                return;
            }
            formData.append("file", fileInput);
        }

        // Lägg till valda källor
        $('input[name="sources"]:checked').each(function () {
            formData.append('sources', $(this).val());
        });
        // Lägg till inputType för backend
        var activeInput = $("#textInputDiv").is(":visible") ? "text" : "file";
        formData.append("inputType", activeInput);
       
        $.ajax({
            url: "/Home/CreateJob",
            type: "POST",
            data: formData,
            processData: false,
            contentType: false,
            success: function (res) {
                totalLinks = res.eNumbers.length;
                foundLinks = 0;
                failedLinks = 0;
                updateLoadingbar();
                preCreateRows(res.eNumbers, res.jobId);
            },
            error: function (xhr) {
                var msg = xhr.responseJSON?.message || "Fel vid skapande av jobb";
                alert(msg);
            }
        });
    });
}

// Pre-create table rows
function preCreateRows(eNumbers, jobId) {
    $("#outputTable").show();
    $("#loadingContainer").show();
    $("#linksCounter").show();

    eNumbers.forEach(num => {
        var row = $("<tr>").attr("data-enumber", num);
        row.append($("<td>").css("position", "relative").text(num));
        row.append($("<td>").css("position", "relative").addClass("source-col").text(""));
        row.append($("<td>").css("position", "relative").text("Hämtar..."));
        row.append($("<td>").css("position", "relative"));   
        $("#results").append(row);
    });
    toggleSources();

    startSSE(jobId);
}

function toggleSources() {
    $("#showSources").change(function () {
        if ($(this).is(":checked")) {
            $(".source-col").show();
        } else {
            $(".source-col").hide();
        }
    });

    // kör vid start också (så det följer default state)
    $("#showSources").trigger("change");
}

// SSE
function startSSE(jobId) {
    var url = "/Home/GetResultsStream?jobId=" + jobId;
    currentSSE = new EventSource(url);
    currentSSE.onmessage = function (e) {
        var result = JSON.parse(e.data);
        var row = $("#results").find(`tr[data-enumber='${result.ENumber}']`);

        // Clear Source + EPD columns
        row.find("td").slice(1).empty();

        if (result.EpdLink && result.EpdLink.startsWith("http")) {
            foundLinks++;
            var sourceTd = row.find("td").eq(1);
            if (result.Source === "Ahlsell") {
                sourceTd.text(result.Source || "").removeClass("text-warning").addClass("text-info");
            } else if (result.Source) {
                sourceTd.text(result.Source).removeClass("text-info").addClass("text-warning");
            } else {
                sourceTd.text("").removeClass("text-info text-warning");
            }

            var epdTd = row.find("td").eq(2);
            epdTd.append(
                $("<a>").addClass("epdLink fs-6 text-wrap").attr("href", result.EpdLink).attr("target", "_blank").text(result.EpdLink)
            );
            var copyTd = row.find("td").eq(3);
            copyTd.empty().append(
                $("<button>").addClass("copyBtn").css({ border: "none", background: "none", cursor: "pointer" })
                    .append($("<img>").attr("src", "/images/copy-white.svg").addClass("ms-2"))
            );
        } else {
            failedLinks++;
            row.find("td").eq(2).text(result.EpdLink || "Ej hittad").addClass("text-danger");
            row.find("td").eq(3).empty();
        }

        updateLoadingbar()

        // Update hidden input
        var existing = $("#resultsInput").val();
        var arr = existing ? JSON.parse(existing) : [];
        arr.push(result);
        $("#resultsInput").val(JSON.stringify(arr));
    };

    currentSSE.addEventListener("done", function () {
        currentSSE.close();
        currentSSE = null;
        $("#downloadForm").show();
        document.getElementById("downloadForm").scrollIntoView({
            behavior: "smooth",
            block: "center"
        });
    });

    currentSSE.onerror = function () {
        $("#results").append("<tr><td colspan='2'>Fel vid hämtning</td></tr>");
        currentSSE.close();
        currentSSE = null;
    };
}

function updateLoadingbar() {
    var percent = Math.round(((foundLinks + failedLinks) / totalLinks) * 100);
    $("#progressBar").css("width", percent + "%");
    $("#progressBarText").text(`${percent} %`);
    $("#progressText").html(`
        <span class="text-light me-3">Hämtade ${foundLinks + failedLinks} av ${totalLinks} länkar</span>
        <span class="text-success me-3">Hittade: ${foundLinks}</span>
        <span class="text-danger me-3">Misslyckade: ${failedLinks}</span>
    `);
}
$("#downloadExcelForm").submit(function (e) {
    e.preventDefault();
    var data = collectResultsFromTable();

    $.ajax({
        url: "/Home/DownloadExcel",
        type: "POST",
        contentType: "application/json",
        data: data,
        xhrFields: { responseType: 'blob' },
        success: function (blob) {
            var link = document.createElement('a');
            link.href = window.URL.createObjectURL(blob);
            link.download = "epd_links.xlsx";
            link.click();
        },
        error: function () {
            alert("Fel vid skapande av Excel-fil");
        }
    });
});
function collectResultsFromTable() {
    var arr = [];
    $("#results tr").each(function () {
        var en = $(this).find("td:first").text().trim();
        var source = $(this).find("td").eq(1).text().trim();
        var link = $(this).find("td").eq(2).find("a").attr("href") || $(this).find("td:last").text().trim();
        arr.push({ ENumber: en, Source: source, EpdLink: link });
    });
    return JSON.stringify(arr);
}

$(document).on("click", "#newSearch", function () {
    reset();
});

function initFileInput() {
    // Klicka på "Välj fil" öppnar filväljaren
    $("#browseBtn").click(function () {
        $("#fileInput").click();
    });

    // Visa filnamn och checkmark när fil väljs
    $("#fileInput").on("change", function () {
        $("#fileName").text(this.files.length > 0 ? this.files[0].name : "").addClass("text-warning");;
        $("#fileCheck").toggle(this.files.length > 0);
        $("#SubmitError").css("visibility", "hidden");
    });

    // Drag & drop highlight
    $("#dropZone").on("dragover", function (e) {
        e.preventDefault();
        $(this).addClass("border-primary bg-light");
        $("#cloud").addClass("text-white");
    });
    $("#dropZone").on("dragleave drop", function (e) {
        e.preventDefault();
        $(this).removeClass("border-primary bg-light");
        $("#cloud").removeClass("text-white");
    });

    // Hantera fil-drop
    $("#dropZone").on("drop", function (e) {
        e.preventDefault();
        const files = e.originalEvent.dataTransfer.files;
        if (files.length > 0) {
            $("#fileInput")[0].files = files; 
            $("#fileName").text(files[0].name).addClass("text-warning");;
            $("#fileCheck").show();
            $("#SubmitError").css("visibility", "hidden");
        }
    });
}
function showCheckmark() {
    // Show green checkmark when a file is selected
    $("#fileInput").on("change", function () {
        if (this.files.length > 0) {
            $("#fileCheck").show();
        } else {
            $("#fileCheck").hide();
        }
    });
}

// Copy buttons
function initCopyButtons() {
    $("#results").on("click", ".copyBtn", function () {
        var btn = $(this);
        var row = btn.closest("tr");
        var link = row.find("a.epdLink").attr("href");
        if (!link) return;

        navigator.clipboard.writeText(link).then(function () {
            let floatMsg = $("<span>")
                .addClass("copyCheckFloating show")
                .text("Kopierat ✔")
                .appendTo("body");

            // positionera mitt över knappen
            let rect = btn[0].getBoundingClientRect();
            floatMsg.css({
                left: rect.left + window.scrollX + rect.width / 2 + 20 + "px",
                top: rect.top + window.scrollY + "px",
                transform: "translateX(-50%)" 
            });

            setTimeout(() => floatMsg.remove(), 1200);
        }).catch(function (err) {
            console.error("Kunde inte kopiera: ", err);
        });
    });
}