import json, sys, sqlite3, urllib.request
API, DB = sys.argv[1], sys.argv[2]
def call(m, p, t=None, b=None):
    r = urllib.request.Request(API+p, method=m, data=json.dumps(b).encode() if b is not None else None)
    r.add_header("Content-Type","application/json")
    if t: r.add_header("Authorization","Bearer "+t)
    with urllib.request.urlopen(r) as x: d=x.read(); return x.status, (json.loads(d) if d else None)
db=sqlite3.connect(DB); q=lambda s: db.execute(s).fetchone()[0]
linea=q("select LineaId from Lineas where Nombre='Tango 2'")
admin=call("POST","/api/auth/login",b={"usuario":"admin","password":"Admin123!"})[1]["accessToken"]
for tipo, rol in [("FALLO EQUIPO","MANTENIMIENTO"),("FALLA PROCESO","SUPERVISOR")]:
    print(call("POST","/api/catalogos/reglas-analisis",admin,{"lineaId":linea,"tipoFallaId":q(f"select TipoFallaId from TiposFalla where Nombre='{tipo}'"),"minutosMinimos":60,"responsableRol":rol,"activo":True})[0])
for u, nombre, rol in [("oper1","Operario (demo)","OPERARIO"),("mant1","Mantenimiento (demo)","MANTENIMIENTO"),("sup1","Supervisor (demo)","SUPERVISOR")]:
    print(call("POST","/api/usuarios",admin,{"userName":u,"email":f"{u}@psag.local","nombreCompleto":nombre,"password":"Clave123!","roles":[rol],"lineasAsignadas":[linea]})[0])
db.execute("update Equipos set Critico=1 where Nombre in ('REBOBINADORA','GOFRADOR')"); db.commit()
print(call("POST","/api/catalogos/tiempos-programados",admin,{"lineaId":linea,"fecha":"2026-10-04","horas":6,"motivo":"Paro parcial por mantenimiento"})[0])
