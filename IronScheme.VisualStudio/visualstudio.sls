
(library (visualstudio)
  (export 
    run-expansion
    read-file
    get-forms
    read-imports)
  (import 
    (ironscheme)
    (ironscheme reader)
    (ironscheme clr))

  (define (read-definitions subst env)
    (let ((lookup (make-eq-hashtable))
          (bindings (make-eq-hashtable)))
      (for-each (lambda (x)
                  (hashtable-set! lookup (cdr x) (car x)))
                subst)
      (for-each (lambda (x)
                  (let ((type (cadr x)))
                    (case type
                      [(global global-macro) 
                        (hashtable-set! bindings 
                                        (hashtable-ref lookup (car x) (ungensym (cddr x)))
                                        type)])))
                env)
      (hashtable-entries bindings)))

  (define (run-expansion e)
    (call-with-values 
      (lambda ()
        (if (= 1 (length e))
            (let-values (((name ver imp* vis* inv* invoke-code visit-code 
                           export-subst export-env guard-code guard-dep*)
                          (core-library-expander (car e))))
              (values export-subst export-env)) 
            (let-values (((lib* invoke-code macro* export-subst export-env) (top-level-expander e)))
              (values export-subst export-env))))
      read-definitions))


  (define (read-file port)
    (let f ((a '()))
      (let ((e (read-annotated port)))
        (if (eof-object? e)
            (reverse a)
            (f (cons e a))))))

  (define (get-forms proc)
    (let-values (((p e) (open-string-output-port)))
      (for-each (lambda (f)
                  (fprintf p "~a\n" f))
                (call-with-values (lambda () (procedure-form proc)) list))
      (e)))
            
  (define (read-imports content)
    (let ((c (car content)))
      (if (= 1 (length content))
          (cdr (cadddr (annotation-stripped c)))
          (cdr (annotation-stripped c)))))
            
)
