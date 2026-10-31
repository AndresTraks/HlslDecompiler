xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 1.5, 0, 0, 0
dcl_texcoord0 r0
/*    0.0 */       exec
/*    2   */          cube r1, r0.zzxy, r0.yxzz
/*    3   */          max r2.__z_, r1.wwww, r1.wwww
              +       rcp r2.x___, r_abs[1].z
/*    4   */          mad r2.xy__, r1.yxxx, r2.xxxx, c255.xxxx
/*    5   */          setTexLOD r0.w
/*    6   */          tfetch2D r1, r0.xyx, tf0, UseRegisterLOD=true
/*    7   */          tfetchCube r0, r2.xyz, tf1, UseComputedLOD=false, UseRegisterLOD=true
/*    0.1 */       alloc colors
/*    1.0 */       exece
/*    8   */          add oC0, r1, r0
/*    1.1 */       cnop
