import sys
from typing import Optional, List, Tuple

# Try to import PySide6 first, then PyQt6
try:
    from PySide6.QtWidgets import QFileDialog, QMessageBox, QColorDialog, QFontDialog, QWidget
    from PySide6.QtGui import QColor, QFont
    QT_LIB = "PySide6"
except ImportError:
    try:
        from PyQt6.QtWidgets import QFileDialog, QMessageBox, QColorDialog, QFontDialog, QWidget
        from PyQt6.QtGui import QColor, QFont
        QT_LIB = "PyQt6"
    except ImportError:
        raise ImportError("Neither PySide6 nor PyQt6 is installed.")

class QtDialogs:
    """
    A utility class to encapsulate common Qt dialogs for simpler usage.
    """

    @staticmethod
    def get_open_filename(parent: Optional[QWidget] = None, 
                          title: str = "Open File", 
                          directory: str = "", 
                          filter: str = "All Files (*)") -> str:
        """Opens a file dialog to select a single file."""
        filename, _ = QFileDialog.getOpenFileName(parent, title, directory, filter)
        return filename

    @staticmethod
    def get_open_filenames(parent: Optional[QWidget] = None, 
                           title: str = "Open Files", 
                           directory: str = "", 
                           filter: str = "All Files (*)") -> List[str]:
        """Opens a file dialog to select multiple files."""
        filenames, _ = QFileDialog.getOpenFileNames(parent, title, directory, filter)
        return filenames

    @staticmethod
    def get_save_filename(parent: Optional[QWidget] = None, 
                          title: str = "Save File", 
                          directory: str = "", 
                          filter: str = "All Files (*)") -> str:
        """Opens a file dialog to save a file."""
        filename, _ = QFileDialog.getSaveFileName(parent, title, directory, filter)
        return filename

    @staticmethod
    def get_existing_directory(parent: Optional[QWidget] = None, 
                               title: str = "Select Directory", 
                               directory: str = "") -> str:
        """Opens a file dialog to select a directory."""
        return QFileDialog.getExistingDirectory(parent, title, directory)

    @staticmethod
    def info(parent: Optional[QWidget] = None, 
             title: str = "Information", 
             message: str = ""):
        """Shows an information message box."""
        QMessageBox.information(parent, title, message)

    @staticmethod
    def warning(parent: Optional[QWidget] = None, 
                title: str = "Warning", 
                message: str = ""):
        """Shows a warning message box."""
        QMessageBox.warning(parent, title, message)

    @staticmethod
    def error(parent: Optional[QWidget] = None, 
              title: str = "Error", 
              message: str = ""):
        """Shows an error message box."""
        QMessageBox.critical(parent, title, message)

    @staticmethod
    def confirm(parent: Optional[QWidget] = None, 
                title: str = "Confirm", 
                message: str = "Are you sure?") -> bool:
        """Shows a question message box with Yes and No buttons."""
        reply = QMessageBox.question(parent, title, message, 
                                     QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No, 
                                     QMessageBox.StandardButton.No)
        return reply == QMessageBox.StandardButton.Yes

    @staticmethod
    def get_color(parent: Optional[QWidget] = None, 
                  initial: QColor = QColor("white"), 
                  title: str = "Select Color") -> Optional[QColor]:
        """Opens a color dialog and returns the selected QColor."""
        color = QColorDialog.getColor(initial, parent, title)
        return color if color.isValid() else None

    @staticmethod
    def get_font(parent: Optional[QWidget] = None, 
                 initial: QFont = QFont(), 
                 title: str = "Select Font") -> Optional[QFont]:
        """Opens a font dialog and returns the selected QFont."""
        ok, font = QFontDialog.getFont(initial, parent, title)
        return font if ok else None
