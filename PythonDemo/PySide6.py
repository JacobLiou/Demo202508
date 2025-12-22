import sys
from qt_dialogs import QtDialogs

# Import QApplication and QWidget from the detected library
try:
    from PySide6.QtWidgets import QApplication, QWidget, QVBoxLayout, QPushButton, QLabel, QHBoxLayout
except ImportError:
    try:
        from PyQt6.QtWidgets import QApplication, QWidget, QVBoxLayout, QPushButton, QLabel, QHBoxLayout
    except ImportError:
        print("Neither PySide6 nor PyQt6 is installed.")
        sys.exit(1)

class DemoWindow(QWidget):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Qt Dialogs Wrapper Demo")
        self.setGeometry(100, 100, 450, 350)

        layout = QVBoxLayout()
        self.setLayout(layout)

        # Title and description
        title_label = QLabel("<h2>Qt Dialogs Wrapper Demo</h2>")
        layout.addWidget(title_label)
        
        self.label = QLabel("Click the buttons below to interact with the encapsulated dialogs.")
        self.label.setWordWrap(True)
        self.label.setStyleSheet("padding: 10px; background-color: #f0f0f0; border-radius: 5px;")
        layout.addWidget(self.label)

        # File Operations Section
        file_group = QVBoxLayout()
        file_label = QLabel("<b>File Operations:</b>")
        file_group.addWidget(file_label)
        
        file_btn_layout = QHBoxLayout()
        btn_open = QPushButton("Open File")
        btn_open.clicked.connect(self.test_open_file)
        file_btn_layout.addWidget(btn_open)

        btn_save = QPushButton("Save File")
        btn_save.clicked.connect(self.test_save_file)
        file_btn_layout.addWidget(btn_save)
        
        btn_dir = QPushButton("Select Folder")
        btn_dir.clicked.connect(self.test_select_dir)
        file_btn_layout.addWidget(btn_dir)
        
        file_group.addLayout(file_btn_layout)
        layout.addLayout(file_group)

        # Messaging Section
        msg_group = QVBoxLayout()
        msg_label = QLabel("<b>Messaging:</b>")
        msg_group.addWidget(msg_label)
        
        msg_btn_layout = QHBoxLayout()
        btn_info = QPushButton("Info")
        btn_info.clicked.connect(lambda: QtDialogs.info(self, "Info", "This is an info message"))
        msg_btn_layout.addWidget(btn_info)

        btn_warn = QPushButton("Warning")
        btn_warn.clicked.connect(lambda: QtDialogs.warning(self, "Warning", "This is a warning message"))
        msg_btn_layout.addWidget(btn_warn)

        btn_err = QPushButton("Error")
        btn_err.clicked.connect(lambda: QtDialogs.error(self, "Error", "This is an error message"))
        msg_btn_layout.addWidget(btn_err)
        
        msg_group.addLayout(msg_btn_layout)
        layout.addLayout(msg_group)

        # Customization Section
        cfg_group = QVBoxLayout()
        cfg_label = QLabel("<b>Customization & Selection:</b>")
        cfg_group.addWidget(cfg_label)
        
        cfg_btn_layout = QHBoxLayout()
        btn_color = QPushButton("Pick Color")
        btn_color.clicked.connect(self.test_color)
        cfg_btn_layout.addWidget(btn_color)

        btn_font = QPushButton("Pick Font")
        btn_font.clicked.connect(self.test_font)
        cfg_btn_layout.addWidget(btn_font)
        
        btn_confirm = QPushButton("Confirm Style Reset")
        btn_confirm.clicked.connect(self.test_confirm)
        cfg_btn_layout.addWidget(btn_confirm)
        
        cfg_group.addLayout(cfg_btn_layout)
        layout.addLayout(cfg_group)

    def test_open_file(self):
        path = QtDialogs.get_open_filename(self)
        self.update_status(f"Open File: {path}" if path else "Canceled open file")

    def test_save_file(self):
        path = QtDialogs.get_save_filename(self)
        self.update_status(f"Save File: {path}" if path else "Canceled save file")

    def test_select_dir(self):
        path = QtDialogs.get_existing_directory(self)
        self.update_status(f"Folder: {path}" if path else "Canceled directory selection")

    def test_color(self):
        color = QtDialogs.get_color(self)
        if color:
            self.update_status(f"Selected Color: {color.name()}")
            self.label.setStyleSheet(f"padding: 10px; background-color: {color.name()}; border-radius: 5px;")
        else:
            self.update_status("Canceled color selection")

    def test_font(self):
        font = QtDialogs.get_font(self)
        if font:
            self.update_status(f"Selected Font: {font.family()}, {font.pointSize()}pt")
            self.label.setFont(font)
        else:
            self.update_status("Canceled font selection")

    def test_confirm(self):
        res = QtDialogs.confirm(self, "Confirm", "Do you want to reset styles to default?")
        if res:
            self.label.setStyleSheet("padding: 10px; background-color: #f0f0f0; border-radius: 5px;")
            self.label.setFont(self.font())
            self.update_status("Styles have been reset.")
        else:
            self.update_status("Reset canceled.")

    def update_status(self, text):
        self.label.setText(text)

if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = DemoWindow()
    window.show()
    sys.exit(app.exec())
