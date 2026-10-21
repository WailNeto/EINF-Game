"""Gibt Ländernamen mit mindestens zehn Buchstaben aus."""

laender = [
	"Afghanistan", "Ägypten", "Albanien", "Algerien", "Andorra", "Angola",
	"Antigua und Barbuda", "Äquatorialguinea", "Argentinien", "Armenien",
	"Aserbaidschan", "Äthiopien", "Australien", "Bahamas", "Bahrain",
	"Bangladesch", "Barbados", "Belgien", "Belize", "Benin", "Bhutan",
	"Bolivien", "Bosnien und Herzegowina", "Botswana", "Brasilien", "Brunei",
	"Bulgarien", "Burkina Faso", "Burundi", "Chile", "China", "Costa Rica",
	"Dänemark", "Deutschland", "Dominica", "Dominikanische Republik",
	"Dschibuti", "Ecuador", "El Salvador", "Elfenbeinküste", "Eritrea",
	"Estland", "Eswatini", "Fidschi", "Finnland", "Frankreich", "Gabun",
	"Gambia", "Georgien", "Ghana", "Grenada", "Griechenland", "Guatemala",
	"Guinea", "Guinea-Bissau", "Guyana", "Haiti", "Honduras", "Indien",
	"Indonesien", "Irak", "Iran", "Irland", "Island", "Israel", "Italien",
	"Jamaika", "Japan", "Jemen", "Jordanien", "Kambodscha", "Kamerun",
	"Kanada", "Kap Verde", "Kasachstan", "Katar", "Kenia", "Kirgisistan",
	"Kiribati", "Kolumbien", "Komoren", "Kongo", "Kroatien", "Kuba",
	"Kuwait", "Laos", "Lesotho", "Lettland", "Libanon", "Liberia", "Libyen",
	"Liechtenstein", "Litauen", "Luxemburg", "Madagaskar", "Malawi",
	"Malaysia", "Malediven", "Mali", "Malta", "Marokko", "Marshallinseln",
	"Mauretanien", "Mauritius", "Mexiko", "Mikronesien", "Moldau", "Monaco",
	"Mongolei", "Montenegro", "Mosambik", "Myanmar", "Namibia", "Nauru",
	"Nepal", "Neuseeland", "Nicaragua", "Niederlande", "Niger", "Nigeria",
	"Nordkorea", "Nordmazedonien", "Norwegen", "Oman", "Österreich", "Pakistan",
	"Palau", "Panama", "Papua-Neuguinea", "Paraguay", "Peru", "Philippinen",
	"Polen", "Portugal", "Ruanda", "Rumänien", "Russland", "Salomonen",
	"Sambia", "Samoa", "San Marino", "Saudi-Arabien", "Schweden", "Schweiz",
	"Senegal", "Serbien", "Seychellen", "Sierra Leone", "Simbabwe", "Singapur",
	"Slowakei", "Slowenien", "Somalia", "Spanien", "Sri Lanka", "St. Kitts und Nevis",
	"St. Lucia", "St. Vincent und die Grenadinen", "Südafrika", "Südkorea",
	"Südsudan", "Sudan", "Suriname", "Syrien", "Tadschikistan", "Tansania",
	"Thailand", "Timor-Leste", "Togo", "Tonga", "Trinidad und Tobago", "Tschad",
	"Tschechien", "Tunesien", "Türkei", "Turkmenistan", "Tuvalu", "Uganda",
	"Ukraine", "Ungarn", "Uruguay", "Usbekistan", "Vanuatu", "Vatikanstadt",
	"Venezuela", "Vereinigte Arabische Emirate", "Vereinigte Staaten",
	"Vereinigtes Königreich", "Vietnam", "Weißrussland",
	"Zentralafrikanische Republik", "Zypern",
]

for land in laender:
	# Es werden nur Buchstaben gezählt; Leerzeichen und Bindestriche nicht.
	anzahl_buchstaben = sum(zeichen.isalpha() for zeichen in land)
	if anzahl_buchstaben >= 10:
		print(land)
