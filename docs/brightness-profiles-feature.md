Feature: Brightness Profiles

Goal

- Allow users to save, load, and delete named brightness profiles.
- Provide a dropdown to select existing profiles and three action buttons to the right: Load (blue disk icon), Save (dark orange disk icon), Delete (red cross icon).
- Show Save (overwrite) and Delete actions only after explicit confirmation.
- New controls appear under the title bar area ("Monitorian" / "Drag here to move the Window") and above the brightness sliders.

User stories

1. As a user, I can select an existing brightness profile from a dropdown and press Load to apply it to the selected monitor(s).
2. As a user, I can save the current brightness settings as a named profile; if the name already exists, I must confirm overwrite.
3. As a user, I can delete an existing profile after a confirmation dialog.
4. As a user, profiles persist across application restarts.

High-level design

- Data model
  - `BrightnessProfile` model: { string Name; IReadOnlyDictionary<string /*device id or monitor identifier*/, int /*brightness*/> Values }
  - Profiles will be stored in user settings as JSON under `Settings.BrightnessProfiles` (or a dedicated file in AppData). Use existing `SettingsCore` access pattern.

- Services
  - `IBrightnessProfileService` with methods: GetAll(), Save(BrightnessProfile), Delete(name), Load(name) (returns profile)
  - Implementation persists JSON to `SettingsCore` or to a file (e.g., `%AppData%\Monitorian\brightness-profiles.json`). Use atomic write.

- ViewModel
  - Add `BrightnessProfilesViewModel` or extend existing `MainWindowViewModel` to expose:
    - `IEnumerable<string> ProfileNames` bound to the dropdown
    - `string SelectedProfileName`
    - `ICommand LoadProfileCommand`, `SaveProfileCommand`, `DeleteProfileCommand`
    - `bool IsProfileSelected` and `IsProfileNameEditable`
  - Save flow: prompt for profile name (input dialog) if not selected or allow editing the dropdown text; if name exists, show confirmation dialog to overwrite.
  - Delete flow: confirmation dialog showing profile name.

- UI changes (MainWindow.xaml)
  - Insert a horizontal panel below the header Border and above the sliders (as requested) containing:
    - ComboBox (editable or with a separate "New" path) bound to `ProfileNames` / `SelectedProfileName`.
    - Buttons to the right with icons: Load (blue disk), Save (dark orange disk), Delete (red cross).
  - Use ToolTip and accessible AutomationProperties for buttons.

- Icons
  - Add three small icon resources to `Monitorian.Core\Resources\Icons` (or reuse existing ones) and reference with Image/Icon controls on the buttons.

- Confirmations
  - Use existing dialog UI patterns in the app (if available) or add a simple MessageBox-based confirmation for overwrite/delete. Modal dialogs must block the operation until user confirms.

- Edge cases
  - If monitors change or selected monitor becomes unavailable, update profile application behavior (skip unavailable monitors).
  - When loading, only apply values for monitors present in the profile; leave others unchanged.
  - Validate brightness range when loading (0..100), and clamp if needed.

Acceptance criteria

- The dropdown and three buttons are visible in the main window in the requested position.
- Profiles can be created, saved (overwrite with confirmation), listed, loaded (applies brightness), and deleted (after confirmation).
- Profiles survive application restart and are stored in user-local persistent storage.
- UI is responsive and shows error messages for I/O failures.

Implementation plan (ordered tasks)

1. Add `BrightnessProfile` model and `IBrightnessProfileService` + file-backed implementation.
2. Add persistence wiring in `SettingsCore` or new storage helper.
3. Add ViewModel properties and commands to `MainWindow`'s view model (or create `BrightnessProfilesViewModel`).
4. Add UI controls to `Monitorian.Core\Views\MainWindow.xaml` in the requested position and bind to the VM.
5. Add icons to resources and wire to the buttons.
6. Implement dialogs for save-overwrite and delete confirmation (use existing dialog helpers if present).
7. Hook load/save/delete commands to service; implement apply/load behaviors in `AppController` (apply to Monitors collection).
8. Add unit tests for service persistence and ViewModel command behaviors.
9. Manual QA: save, overwrite, load partial profile, delete, restart app and verify persistence.

Notes and constraints

- Target framework is .NET Framework 4.8. Use compatible JSON serializer (System.Text.Json is not available by default). Use `Newtonsoft.Json` (Json.NET) which is already frequently used in similar projects; if not present, add as a package.
- Keep UI XAML changes minimal and consistent with existing styles (use `App.Background.Plain`, `App.Foreground`, and existing button styles).

Next step

- Implement step 1 and 3 in code: add `BrightnessProfile` model and the ViewModel properties/commands. Optionally create the UI placeholder in `MainWindow.xaml` to reserve space for the controls.

Estimated effort

- 1–2 days for full feature (including persistence, dialogs, wiring to monitors, icons and QA). 2–3 hours to implement the first UI placeholder + model + service skeleton.
