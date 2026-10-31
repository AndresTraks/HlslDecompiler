xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 1.5, 0, 0, 0
dcl_texcoord0 r0.xyz
/*    0.0 */       exec
/*    2   */          cube r1, r0.zzxy, r0.yxzz
/*    3   */          max r2.__z_, r1.wwww, r1.wwww
              +       rcp r0.___w, r_abs[1].z
/*    4   */          mad r2.xy__, r1.yxxx, r0.wwww, c255.xxxx
/*    5   */          tfetch1D r1, r0.xyz, tf2
/*    6   */          tfetchCube r2, r2.xyz, tf0
/*    7   */          tfetch3D r0, r0.xyz, tf1
/*    0.1 */       alloc colors
/*    1.0 */       exece
/*    8   */          add r0, r2, r0
/*    9   */          add oC0, r0, r1
/*    1.1 */       cnop
