xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=0
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 2, 0, 1, 0
dcl_texcoord0 r0.xy
/*    0.0 */       exec
/*    2   */          getCompTexLOD2D r0.__x_, r0.yx, tf0
/*    0.1 */       alloc colors
/*    1.0 */       exece
/*    3   */          sgt r0.x___, c255.zzzz, r0.zzzz
/*    4   */          sgt r0._y__, c255.xxxx, r0.zzzz
/*    5   */          sge r0.__z_, r0.zzzz, c255.xxxx
/*    6   */          cndgt r0._yz_, r0.xxxx, c255.yyyy, r0.yyzz
/*    7   */          max oC0.xyz1, r0.xyzz, r0.xyzz
/*    1.1 */       cnop
