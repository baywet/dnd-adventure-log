window.registerDropZone = (dropZoneElement, dotNetHelper) => {
    dropZoneElement.addEventListener('dragover', function (e) {
        e.preventDefault();
        dropZoneElement.classList.add('dragover');
    });
    dropZoneElement.addEventListener('dragleave', function (e) {
        e.preventDefault();
        dropZoneElement.classList.remove('dragover');
    });
    dropZoneElement.addEventListener('drop', function (e) {
        e.preventDefault();
        dropZoneElement.classList.remove('dragover');
        const files = Array.from(e.dataTransfer.files).filter(f => f.type === 'audio/mpeg');
        if (files.length > 0) {
            dotNetHelper.invokeMethodAsync('OnFilesDropped', files.map(f => f.name));
        }
    });
};
