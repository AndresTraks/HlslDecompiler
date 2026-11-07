xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
def c252, 0, 0, 0, 0
def c253, 2, 0, 0, 0
def c254, -0.5, -0.25, -0.75, -1
def c255, 0.25, 0.5, -1, -0.75
dcl_texcoord0 r0
/*    0.0 */       exec    // PredicateClean=false
/*    6   */          setp_ge_push r1.x___, c253.yyyy, r0.yyyy
              +       sges r1._y__, r0.y
/*    7   */          setp_rstr r1.x___, r1.x
/*    0.1 */       loop i31, L4
                label L2
/*    1.0 */       exec    // PredicateClean=false
/*    8   */          setp_gt_push r1.x___, r1.xxxx, r0.zzzz
/*    9   */     (p0) add r0, r0, c255
/*   10   */          setp_pop r1.x___, r1.x
/*   11   */     (p0) sgts r1._y__, r0.w
/*   12   */     (p0) setp_gt r0._, r0.w
/*   13   */    (!p0) setp_clr r1.x___
/*    1.1 */ (!p0) endloop i31, L2
                label L4
/*    2.0 */       exec    // PredicateClean=false
/*   14   */          setp_eq_push r1.x___, c253.yyyy, r1.yyyy
/*   15   */          setp_rstr r1.x___, r1.x
/*    2.1 */       loop i30, L9
                label L6
/*    3.0 */       exec    // PredicateClean=false
/*   16   */     (p0) sgt r1.__z_, r0.xxxx, c253.xxxx
/*   17   */          setp_ne_push r1.x___, r1.xxxx, r1.zzzz
/*   18   */     (p0) add r0, r0, c254
/*   19   */     (p0) sgts r1._y__, r0.y
/*   20   */          setp_pop r1.x___, r1.x
/*   21   */     (p0) setp_eq r0._, r1.y
/*    3.1 */ (!p0) exec    // PredicateClean=false
/*   22   */    (!p0) setp_clr r1.x___
/*    4.0 */ (!p0) endloop i30, L6
                label L9
/*    4.1 */       alloc colors
/*    5.0 */       exece
/*   23   */          add oC0, r0, r1.yyyy
/*    5.1 */       cnop
