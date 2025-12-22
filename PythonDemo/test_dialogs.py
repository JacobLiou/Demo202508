import sys
from qt_dialogs import QtDialogs

# Import QApplication and QWidget from the detected library
try:
    from PySide6.QtWidgets import QApplication, QWidget, QVBoxLayout, QPushButton, QLabel, QHBoxLayout
except ImportError:
    from PyQt6.QtWidgets import QApplication, QWidget, QVBoxLayout, QPushButton, QLabel, QHBoxLayout

class DemoWindow(QWidget):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Qt Dialogs Wrapper Demo")
        self.setGeometry(100, 100, 400, 300)

        layout = QVBoxLayout()
        self.setLayout(layout)

        self.label = QLabel("Click buttons to test dialogs")
        self.label.setWordWrap(True)
        layout.addWidget(self.label)

        # File Dialogs
        file_layout = QHBoxLayout()
        btn_open = QPushButton("Open File")
        btn_open.clicked.connect(self.test_open_file)
        file_layout.addWidget(btn_open)

        btn_save = QPushButton("Save File")
        btn_save.clicked.connect(self.test_save_file)
        file_layout.addWidget(btn_save)
        layout.addLayout(file_layout)

        # Message Boxes
        msg_layout = QHBoxLayout()
        btn_info = QPushButton("Info")
        btn_info.clicked.connect(lambda: QtDialogs.info(self, "Info", "This is an info message"))
        msg_layout.addWidget(btn_info)

        btn_warn = QPushButton("Warning")
        btn_warn.clicked.connect(lambda: QtDialogs.warning(self, "Warning", "This is a warning message"))
        msg_layout.addWidget(btn_warn)

        btn_err = QPushButton("Error")
        btn_err.clicked.connect(lambda: QtDialogs.error(self, "Error", "This is an error message"))
        msg_layout.addWidget(btn_err)
        layout.addLayout(msg_layout)

        # Color and Font
        cfg_layout = QHBoxLayout()
        btn_color = QPushButton("Pick Color")
        btn_color.clicked.connect(self.test_color)
        cfg_layout.addWidget(btn_color)

        btn_font = QPushButton("Pick Font")
        btn_font.clicked.connect(self.test_font)
        cfg_layout.addWidget(btn_font)
        layout.addLayout(cfg_layout)

        # Confirm
        btn_confirm = QPushButton("Test Confirm")
        btn_confirm.clicked.connect(self.test_confirm)
        layout.addWidget(btn_confirm)

    def test_open_file(self):
        path = QtDialogs.get_open_filename(self)
        self.label.setText(f"Open File: {path}" if path else "No file selected")

    def test_save_file(self):
        path = QtDialogs.get_save_filename(self)
        self.label.setText(f"Save File: {path}" if path else "No file selected")

    def test_color(self):
        color = QtDialogs.get_color(self)
        if color:
            self.label.setText(f"Selected Color: {color.name()}")
            self.label.setStyleSheet(f"color: {color.name()};")

    def test_font(self):
        font = QtDialogs.get_font(self)
        if font:
            self.label.setText(f"Selected Font: {font.family()}, {font.pointSize()}pt")
            self.label.setFont(font)

    def test_confirm(self):
        res = QtDialogs.confirm(self, "Confirm", "Do you want to reset styles?")
        if res:
            self.label.setStyleSheet("")
            self.label.setFont(self.font())
            self.label.setText("Styles reset")

if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = DemoWindow()
    window.show()
    sys.exit(app.exec())
