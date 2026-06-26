import math

# ---------- Standard drag tables (public-domain BRL G1 / G7 reference data) ----------
G1 = [
(0.00,0.2629),(0.05,0.2558),(0.10,0.2487),(0.15,0.2413),(0.20,0.2344),(0.25,0.2278),
(0.30,0.2214),(0.35,0.2155),(0.40,0.2104),(0.45,0.2061),(0.50,0.2032),(0.55,0.2020),
(0.60,0.2034),(0.70,0.2165),(0.725,0.2230),(0.75,0.2313),(0.775,0.2417),(0.80,0.2546),
(0.825,0.2706),(0.85,0.2901),(0.875,0.3136),(0.90,0.3415),(0.925,0.3734),(0.95,0.4084),
(0.975,0.4448),(1.0,0.4805),(1.025,0.5136),(1.05,0.5427),(1.075,0.5677),(1.10,0.5883),
(1.125,0.6053),(1.15,0.6191),(1.20,0.6393),(1.25,0.6518),(1.30,0.6589),(1.35,0.6621),
(1.40,0.6625),(1.45,0.6607),(1.50,0.6573),(1.55,0.6528),(1.60,0.6474),(1.65,0.6413),
(1.70,0.6347),(1.75,0.6280),(1.80,0.6210),(1.85,0.6141),(1.90,0.6072),(1.95,0.6003),
(2.00,0.5934),(2.05,0.5867),(2.10,0.5804),(2.15,0.5743),(2.20,0.5685),(2.25,0.5630),
(2.30,0.5577),(2.35,0.5527),(2.40,0.5481),(2.45,0.5438),(2.50,0.5397),(2.60,0.5325),
(2.70,0.5264),(2.80,0.5211),(2.90,0.5168),(3.00,0.5133),(3.10,0.5105),(3.20,0.5084),
(3.30,0.5067),(3.40,0.5054),(3.50,0.5040),(3.60,0.5030),(3.70,0.5022),(3.80,0.5016),
(3.90,0.5010),(4.00,0.5006),(4.20,0.4998),(4.40,0.4995),(4.60,0.4992),(4.80,0.4990),(5.00,0.4988),
]
G7 = [
(0.00,0.1198),(0.05,0.1197),(0.10,0.1196),(0.15,0.1194),(0.20,0.1193),(0.25,0.1194),
(0.30,0.1194),(0.35,0.1194),(0.40,0.1193),(0.45,0.1193),(0.50,0.1194),(0.55,0.1193),
(0.60,0.1194),(0.65,0.1197),(0.70,0.1202),(0.725,0.1207),(0.75,0.1215),(0.775,0.1226),
(0.80,0.1242),(0.825,0.1266),(0.85,0.1306),(0.875,0.1368),(0.90,0.1464),(0.925,0.1660),
(0.95,0.2054),(0.975,0.2993),(1.0,0.3803),(1.025,0.4015),(1.05,0.4043),(1.075,0.4034),
(1.10,0.4014),(1.125,0.3987),(1.15,0.3955),(1.20,0.3884),(1.25,0.3810),(1.30,0.3732),
(1.35,0.3657),(1.40,0.3580),(1.50,0.3440),(1.55,0.3376),(1.60,0.3315),(1.65,0.3260),
(1.70,0.3209),(1.75,0.3160),(1.80,0.3117),(1.85,0.3078),(1.90,0.3042),(1.95,0.3010),
(2.00,0.2980),(2.05,0.2951),(2.10,0.2922),(2.15,0.2892),(2.20,0.2864),(2.25,0.2835),
(2.30,0.2807),(2.35,0.2779),(2.40,0.2752),(2.45,0.2725),(2.50,0.2697),(2.55,0.2670),
(2.60,0.2643),(2.65,0.2615),(2.70,0.2588),(2.75,0.2561),(2.80,0.2533),(2.85,0.2506),
(2.90,0.2479),(2.95,0.2451),(3.00,0.2424),(3.10,0.2368),(3.20,0.2313),(3.30,0.2258),
(3.40,0.2205),(3.50,0.2154),(3.60,0.2106),(3.70,0.2060),(3.80,0.2017),(3.90,0.1975),
(4.00,0.1935),(4.20,0.1861),(4.40,0.1793),(4.60,0.1730),(4.80,0.1672),(5.00,0.1618),
]

def cd_lookup(table, mach):
    if mach <= table[0][0]: return table[0][1]
    if mach >= table[-1][0]: return table[-1][1]
    for i in range(1,len(table)):
        if mach < table[i][0]:
            m0,c0 = table[i-1]; m1,c1 = table[i]
            return c0 + (c1-c0)*(mach-m0)/(m1-m0)
    return table[-1][1]

# ---------- units ----------
FT=0.3048; IN=0.0254; YD=0.9144; GRAIN_KG=6.479891e-5; LBIN2_KGM2=703.069
G=9.80665

def air_density(temp_c, pressure_inhg, humidity_pct):
    P = pressure_inhg*3386.389  # Pa
    T = temp_c+273.15
    # saturation vapor pressure (Pa), Tetens
    Psat = 610.78*math.exp(17.27*temp_c/(temp_c+237.3))
    Pv = (humidity_pct/100.0)*Psat
    Pd = P - Pv
    rho = Pd/(287.058*T) + Pv/(461.495*T)
    return rho

def speed_of_sound(temp_c):
    return math.sqrt(1.4*287.058*(temp_c+273.15))

# ---------- 3DOF point-mass solver ----------
# axes: x downrange, y up, z right (crosswind +z = wind blowing toward shooter's right pushes -z etc handled by wind vector)
def solve(bc, drag_table, mv_fps, launch_angle_rad, zero_dummy,
          temp_c, press_inhg, hum, wind_mps, wind_from_deg,
          sight_height_in, max_range_yd, step_yd):
    rho = air_density(temp_c,press_inhg,hum)
    a_snd = speed_of_sound(temp_c)
    bc_si = bc*LBIN2_KGM2
    v0 = mv_fps*FT
    # wind vector: meteorological "from" convention. 90deg = from shooter's left -> pushes bullet +z (right)
    wr = math.radians(wind_from_deg)
    # downrange component (headwind +) and cross component
    wind_x = -wind_mps*math.cos(wr)   # wind from 0deg (downrange ahead) = headwind -> opposes +x
    wind_z = -wind_mps*math.sin(wr)
    state=[0.0,-sight_height_in*IN,0.0, v0*math.cos(launch_angle_rad),v0*math.sin(launch_angle_rad),0.0]
    def deriv(s):
        x,y,z,vx,vy,vz=s
        rvx=vx-wind_x; rvy=vy; rvz=vz-wind_z
        v=math.sqrt(rvx*rvx+rvy*rvy+rvz*rvz)
        mach=v/a_snd
        cd=cd_lookup(drag_table,mach)
        k=math.pi*rho*cd/(8.0*bc_si)   # a_drag = k * v * rvec
        ax=-k*v*rvx; ay=-k*v*rvy-G; az=-k*v*rvz
        return [vx,vy,vz,ax,ay,az]
    dt=0.0005
    out=[]
    targets=[i for i in range(0,int(max_range_yd)+1,int(step_yd))]
    ti=0
    t=0.0
    prev=state[:]
    while ti<len(targets) and t<20:
        tx=targets[ti]*YD
        # RK4 step
        k1=deriv(state)
        k2=deriv([state[i]+0.5*dt*k1[i] for i in range(6)])
        k3=deriv([state[i]+0.5*dt*k2[i] for i in range(6)])
        k4=deriv([state[i]+dt*k3[i] for i in range(6)])
        newstate=[state[i]+dt/6*(k1[i]+2*k2[i]+2*k3[i]+k4[i]) for i in range(6)]
        if newstate[0]>=tx:
            # interpolate at tx
            f=(tx-state[0])/(newstate[0]-state[0]) if newstate[0]!=state[0] else 0
            xs=[state[i]+f*(newstate[i]-state[i]) for i in range(6)]
            v=math.sqrt(xs[3]**2+xs[4]**2+xs[5]**2)
            out.append((targets[ti], xs[1], xs[2], v, t+f*dt))
            ti+=1
        prev=state
        state=newstate
        t+=dt
    return out,rho,a_snd

def find_zero_angle(bc,table,mv,zero_yd,temp,press,hum,sh):
    lo,hi=-0.02,0.05
    for _ in range(60):
        mid=(lo+hi)/2
        o,_,_=solve(bc,table,mv,mid,0,temp,press,hum,0,0,sh,zero_yd,zero_yd)
        if not o: 
            lo=mid; continue
        drop=o[-1][1]
        if drop>0: hi=mid
        else: lo=mid
    return (lo+hi)/2

# ---------- TEST: 6.5 Creedmoor 140gr ELD-M ----------
bc_g7=0.315
mv=2710
sh=2.0
zero=100
ang=find_zero_angle(bc_g7,G7,mv,zero,15,29.92,50,sh)
res,rho,asnd=solve(bc_g7,G7,mv,ang,0,15,29.92,50,0,0,sh,1000,100)
print(f"rho={rho:.4f} kg/m3  a_sound={asnd:.1f} m/s  zero_angle={math.degrees(ang)*60:.2f} MOA")
print(f"{'yd':>5}{'drop_in':>10}{'MIL':>8}{'MOA':>8}{'vel_fps':>9}{'ToF_s':>8}")
for r,y,z,v,t in res:
    drop_in=y/IN
    rng_in=r*YD/IN
    mil = -(y/(r*YD))*1000 if r>0 else 0
    moa = -(y/(r*YD))*(180/math.pi*60) if r>0 else 0
    print(f"{r:>5}{drop_in:>10.1f}{mil:>8.2f}{moa:>8.2f}{v/FT:>9.0f}{t:>8.3f}")

print("\n--- long range effects (6.5CM 140gr, 8\" twist, len 1.345in, lat 42N, az 90deg) ---")
def miller_sg(weight_gr, dia_in, length_in, twist_in, mv_fps, temp_f, press_inhg):
    t = twist_in/dia_in
    l = length_in/dia_in
    sg = 30.0*weight_gr/(t*t*dia_in**3*l*(1+l*l))
    # velocity correction
    sg *= (mv_fps/2800.0)**(1.0/3.0)
    # atmosphere correction (to std 59F, 29.92)
    sg *= ((temp_f+460.0)/519.0)*(29.92/press_inhg)
    return sg

sg = miller_sg(140,0.264,1.345,8.0,2710,59,29.92)
print(f"Miller SG = {sg:.2f}")

# Litz spin drift: SD(in) = 1.25*(SG+1.2)*ToF^1.83
for r,y,z,v,t in res:
    if r in (500,1000):
        sd = 1.25*(sg+1.2)*(t**1.83)
        sd_mil = (sd*IN/(r*YD))*1000
        print(f"  {r}yd  spin_drift={sd:.1f}in ({sd_mil:.2f} MIL)  ToF={t:.3f}s")

# Coriolis (horizontal): drift = Omega*ToF^2*sin(lat) component ; vertical (Eotvos)
OMEGA=7.292115e-5
lat=math.radians(42); az=math.radians(90)
for r,y,z,v,t in res:
    if r in (500,1000):
        rng=r*YD
        # horizontal coriolis deflection (m), simplified McCoy
        horiz = OMEGA*rng*t*math.sin(lat)   # approx lateral
        horiz_in=horiz/IN
        horiz_mil=(horiz/rng)*1000
        print(f"  {r}yd  coriolis_horiz~{horiz_in:.1f}in ({horiz_mil:.2f} MIL)")
