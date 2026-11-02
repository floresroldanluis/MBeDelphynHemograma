param([Reflection.Assembly]$Assembly)
# Offline UI tests inject a fixed catalog; SQL name resolution is covered by verify-configuration.
$settingsType=$Assembly.GetType('Hemograma.TestSettings')
$testFlags=[Reflection.BindingFlags]'Static,NonPublic'
$legacy=$settingsType.GetField('LegacyDefaultValue',$testFlags).GetRawConstantValue()
$definitions=$settingsType.GetMethod('Parse',$testFlags).Invoke($null,[object[]]@($legacy))
$settingsType.GetField('loaded',$testFlags).SetValue($null,$definitions)
