xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=3
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 0.5, 0, 0, 0
dcl_texcoord0 r0.xy
/*    0.0 */       exec
/*    4   */          sgt r1, -r_abs[0].xxxx, c255.yyyy
/*    5   */          sgt r2, -r_abs[0].xxxx, c255.yyyy
/*    0.1 */       loop i16, L4
                label L2
/*    1.0 */       exec
/*    6   */          add r0.__zw, r0.xxxy, c[0+aL].xxxy
/*    7   */          tfetch2D r3, r0.zwz, tf0
/*    8   */          serialize
                      mad r2, r3.wzyx, c[0+aL].zzzz, r1.wzyx
/*    9   */          mad r1, r3, c[0+aL].zzzz, r1
/*    1.1 */       endloop i16, L2
                label L4
/*    2.0 */       exec    // PredicateClean=false
/*   10   */          sgt r1.x___, r2.xxxx, c255.xxxx
/*   11   */          max r0, r2.wzyx, r2.wzyx
              +       setp_ne r0._, r1.x
/*   12   */     (p0) sqrt r0.x___, r_abs[0].x
/*   13   */     (p0) sqrt r0._y__, r_abs[0].y
/*   14   */     (p0) sqrt r0.__z_, r_abs[0].z
/*    2.1 */       alloc colors
/*    3.0 */       exece
/*   15   */          max oC0, r0, r0
/*    3.1 */       cnop
