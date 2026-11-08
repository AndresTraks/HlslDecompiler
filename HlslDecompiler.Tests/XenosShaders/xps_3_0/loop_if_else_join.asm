xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 1, 0, 0, 0
dcl_texcoord0 r0.xyz
/*    0.0 */       exec
/*    6   */          sgt r1, -r_abs[0].xxxx, c255.yyyy
/*    0.1 */       loop i31, L9
                label L2
/*    1.0 */       exec    // PredicateClean=false
/*    7   */          setp_ne r0._, r1.y
/*    1.1 */ (!p0) jmp L6
/*    2.0 */       exec
/*    8   */          max r2.xyz_, c1.xyzz, c1.xyzz
/*    2.1 */       jmp L7
                label L6
/*    3.0 */       exec
/*    9   */          max r2.xyz_, c0.xyzz, c0.xyzz
                label L7
/*    3.1 */       exec
/*   10   */          addsc r1._y__, c255.x, r1.y
/*   11   */          mad r1.x_zw, r2.xyyz, r0.xyyz, r1.xzzw
/*    4.0 */       endloop i31, L2
                label L9
/*    4.1 */       alloc colors
/*    5.0 */       exece
/*   12   */          max oC0.xyz1, r1.xzww, r1.xzww
/*    5.1 */       cnop
